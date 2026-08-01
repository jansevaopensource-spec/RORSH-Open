using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Websocket.Client;

namespace RCS
{
    public class RcsWorker : BackgroundService
    {
        private readonly ILogger<RcsWorker> _logger;
        private static readonly string ServerUrl = "wss://rorsh-openweb-ssh.onrender.com";
        private WebsocketClient _client;
        private readonly byte[] _keyBuffer = new byte[32];
        private string _rorshKey;
        private bool _isConnected = false;
        private bool _sessionActive = false;
        private Process _currentProcess;
        private readonly object _processLock = new object();

        public RcsWorker(ILogger<RcsWorker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RCS starting...");

            // Derive encryption key (must match server key derivation)
            using (var sha256 = SHA256.Create())
            {
                byte[] keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes("rorsh-default-key-2024"));
                Array.Copy(keyBytes, _keyBuffer, 32);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!_isConnected)
                    {
                        await ConnectToServer();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Connection error: " + ex.Message);
                }

                await Task.Delay(5000, stoppingToken);
            }

            _client?.Dispose();
            _logger.LogInformation("RCS stopped.");
        }

        private async Task ConnectToServer()
        {
            try
            {
                var url = new Uri(ServerUrl);
                _client = new WebsocketClient(url);
                _client.ReconnectTimeout = TimeSpan.FromSeconds(30);
                _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(30);

                _client.MessageReceived.Subscribe(msg =>
                {
                    try
                    {
                        string decrypted = Decrypt(msg.Text);
                        var data = JsonSerializer.Deserialize<JsonElement>(decrypted);
                        HandleServerMessage(data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Message processing error: " + ex.Message);
                    }
                });

                _client.DisconnectionHappened.Subscribe(info =>
                {
                    _logger.LogWarning("Disconnected from server. Reconnecting...");
                    _isConnected = false;
                    _sessionActive = false;
                    KillCurrentProcess();
                });

                await _client.Start();
                _isConnected = true;

                // Register client
                string hostname = Dns.GetHostName();
                string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";

                var registerPayload = new
                {
                    type = "register_client",
                    hostname = hostname,
                    platform = platform
                };

                await SendCommand(registerPayload);
                _logger.LogInformation("Connected and registered with server.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to connect: " + ex.Message);
                _isConnected = false;
            }
        }

        private void HandleServerMessage(JsonElement data)
        {
            string msgType = data.GetProperty("type").GetString();

            switch (msgType)
            {
                case "registered":
                    _rorshKey = data.GetProperty("rorshKey").GetString();
                    _logger.LogInformation("Registered with rorshKey: " + _rorshKey);
                    break;

                case "session_start":
                    _sessionActive = true;
                    _logger.LogInformation("Admin session started.");
                    break;

                case "session_end":
                    _sessionActive = false;
                    KillCurrentProcess();
                    _logger.LogInformation("Admin session ended.");
                    break;

                case "execute":
                    if (_sessionActive)
                    {
                        string command = data.GetProperty("command").GetString();
                        _ = Task.Run(() => ExecuteCommand(command));
                    }
                    break;

                case "error":
                    _logger.LogError("Server error: " + data.GetProperty("message").GetString());
                    break;
            }
        }

        private async Task ExecuteCommand(string command)
        {
            try
            {
                lock (_processLock)
                {
                    KillCurrentProcess();
                }

                string shell;
                string shellArg;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    shell = "cmd.exe";
                    shellArg = "/c";
                }
                else
                {
                    shell = "/bin/bash";
                    shellArg = "-c";
                }

                var psi = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = shellArg + " "" + command + """,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };

                _currentProcess = new Process { StartInfo = psi };

                _currentProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _ = SendCommand(new { type = "command_output", output = e.Data + "\n" });
                    }
                };

                _currentProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _ = SendCommand(new { type = "command_error", error = e.Data });
                    }
                };

                _currentProcess.Start();
                _currentProcess.BeginOutputReadLine();
                _currentProcess.BeginErrorReadLine();
                await _currentProcess.WaitForExitAsync();

                int exitCode = _currentProcess.ExitCode;
                await SendCommand(new { type = "command_exit", code = exitCode });
            }
            catch (Exception ex)
            {
                await SendCommand(new { type = "command_error", error = ex.Message });
                await SendCommand(new { type = "command_exit", code = -1 });
            }
            finally
            {
                lock (_processLock)
                {
                    _currentProcess = null;
                }
            }
        }

        private void KillCurrentProcess()
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    _currentProcess.Kill(true);
                    _currentProcess.Dispose();
                    _currentProcess = null;
                }
            }
            catch { }
        }

        private async Task SendCommand(object data)
        {
            if (_client == null || !_isConnected) return;
            string json = JsonSerializer.Serialize(data);
            string encrypted = Encrypt(json);
            _client.Send(encrypted);
            await Task.Delay(10);
        }

        private string Encrypt(string plainText)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _keyBuffer;
                aes.GenerateIV();
                aes.Mode = CipherMode.GCM;
                aes.Padding = PaddingMode.None;

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                byte[] result = new byte[aes.IV.Length + cipherBytes.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

                return Convert.ToHexString(result);
            }
        }

        private string Decrypt(string cipherText)
        {
            byte[] fullBytes = Convert.FromHexString(cipherText);
            byte[] iv = new byte[12];
            byte[] cipherBytes = new byte[fullBytes.Length - 12];

            Buffer.BlockCopy(fullBytes, 0, iv, 0, 12);
            Buffer.BlockCopy(fullBytes, 12, cipherBytes, 0, cipherBytes.Length);

            using (var aes = Aes.Create())
            {
                aes.Key = _keyBuffer;
                aes.IV = iv;
                aes.Mode = CipherMode.GCM;
                aes.Padding = PaddingMode.None;

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
        }
    }
}
