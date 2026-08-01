using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;

namespace RAS
{
    class Program
    {
        private static readonly string ServerUrl = "wss://rorsh-openweb-ssh.onrender.com";
        private static WebsocketClient _client;
        private static bool _isAuthenticated = false;
        private static bool _isConnected = false;
        private static string _currentClientKey = null;
        private static readonly byte[] KeyBuffer = new byte[32];
        private static readonly object ConsoleLock = new object();

        static async Task Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  RAS - RORSH Admin Shell v1.0");
            Console.WriteLine("========================================");
            Console.WriteLine("");
            Console.WriteLine("Type 'RAS-Start' to connect to server");
            Console.WriteLine("Type 'exit' to quit");
            Console.WriteLine("");

            while (true)
            {
                Console.Write("RAS> ");
                string input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input)) continue;

                if (input.ToLower() == "exit")
                {
                    if (_client != null)
                    {
                        _client.Dispose();
                    }
                    break;
                }

                if (input.ToLower() == "ras-start")
                {
                    if (_isConnected)
                    {
                        Console.WriteLine("Already connected to server.");
                        continue;
                    }
                    await StartConnection();
                    continue;
                }

                if (input.ToLower() == "ras-stop")
                {
                    if (!_isConnected)
                    {
                        Console.WriteLine("Not connected to server.");
                        continue;
                    }
                    StopConnection();
                    continue;
                }

                if (input.ToLower() == "c-list")
                {
                    if (!_isAuthenticated)
                    {
                        Console.WriteLine("Not authenticated. Use RAS-Start first.");
                        continue;
                    }
                    await SendCommand(new { type = "command", command = "c-list" });
                    continue;
                }

                if (input.ToLower().StartsWith("get-connect "))
                {
                    if (!_isAuthenticated)
                    {
                        Console.WriteLine("Not authenticated. Use RAS-Start first.");
                        continue;
                    }
                    string key = input.Substring("get-connect ".Length).Trim();
                    if (string.IsNullOrEmpty(key))
                    {
                        Console.WriteLine("Usage: get-connect @rorshkey");
                        continue;
                    }
                    if (key.StartsWith("@")) key = key.Substring(1);
                    _currentClientKey = key;
                    await SendCommand(new { type = "get-connect", rorshKey = key });
                    continue;
                }

                if (input.ToLower() == "get-disconnect")
                {
                    if (!_isAuthenticated || string.IsNullOrEmpty(_currentClientKey))
                    {
                        Console.WriteLine("No active client connection.");
                        continue;
                    }
                    await SendCommand(new { type = "get-disconnect", rorshKey = _currentClientKey });
                    _currentClientKey = null;
                    continue;
                }

                // If connected to a client, relay command
                if (!string.IsNullOrEmpty(_currentClientKey) && _isAuthenticated)
                {
                    await SendCommand(new { type = "relay_command", rorshKey = _currentClientKey, command = input });
                }
                else
                {
                    Console.WriteLine("Unknown command. Available commands: RAS-Start, RAS-Stop, c-list, get-connect @key, get-disconnect, exit");
                }
            }
        }

        static async Task StartConnection()
        {
            try
            {
                Console.Write("Enter Admin ID: ");
                string adminId = Console.ReadLine()?.Trim();
                Console.Write("Enter Password: ");
                string password = ReadPassword();
                Console.WriteLine();

                // Derive key from password for encryption
                using (var sha256 = SHA256.Create())
                {
                    byte[] keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "rorsh-salt-2024"));
                    Array.Copy(keyBytes, KeyBuffer, 32);
                }

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
                        lock (ConsoleLock)
                        {
                            Console.WriteLine("Error processing message: " + ex.Message);
                        }
                    }
                });

                _client.DisconnectionHappened.Subscribe(info =>
                {
                    lock (ConsoleLock)
                    {
                        Console.WriteLine("Disconnected from server.");
                    }
                    _isConnected = false;
                    _isAuthenticated = false;
                    _currentClientKey = null;
                });

                await _client.Start();
                _isConnected = true;

                // Authenticate
                var authPayload = new { type = "auth_admin", adminId = adminId, password = password };
                await SendCommand(authPayload);

                Console.WriteLine("Connected to server. Authenticating...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Connection failed: " + ex.Message);
                _isConnected = false;
            }
        }

        static void StopConnection()
        {
            _client?.Dispose();
            _client = null;
            _isConnected = false;
            _isAuthenticated = false;
            _currentClientKey = null;
            Console.WriteLine("Disconnected from server.");
        }

        static async Task SendCommand(object data)
        {
            if (_client == null || !_isConnected) return;
            string json = JsonSerializer.Serialize(data);
            string encrypted = Encrypt(json);
            _client.Send(encrypted);
            await Task.Delay(10);
        }

        static void HandleServerMessage(JsonElement data)
        {
            string msgType = data.GetProperty("type").GetString();

            lock (ConsoleLock)
            {
                switch (msgType)
                {
                    case "auth_success":
                        _isAuthenticated = true;
                        Console.WriteLine("Authentication successful.");
                        break;

                    case "auth_failed":
                        _isAuthenticated = false;
                        Console.WriteLine("Authentication failed: " + data.GetProperty("message").GetString());
                        break;

                    case "client_list":
                        Console.WriteLine("\n--- Connected Clients ---");
                        var clients = data.GetProperty("clients");
                        if (clients.GetArrayLength() == 0)
                        {
                            Console.WriteLine("No clients connected.");
                        }
                        else
                        {
                            foreach (var client in clients.EnumerateArray())
                            {
                                Console.WriteLine("RorshKey: " + client.GetProperty("rorshKey").GetString() +
                                    " | Host: " + client.GetProperty("hostname").GetString() +
                                    " | IP: " + client.GetProperty("ip").GetString() +
                                    " | Platform: " + client.GetProperty("platform").GetString());
                            }
                        }
                        Console.WriteLine("------------------------\n");
                        break;

                    case "client_connected":
                        Console.WriteLine("[+] Client connected: " + data.GetProperty("rorshKey").GetString() +
                            " (" + data.GetProperty("hostname").GetString() + ")");
                        break;

                    case "client_disconnected":
                        Console.WriteLine("[-] Client disconnected: " + data.GetProperty("rorshKey").GetString());
                        break;

                    case "connected":
                        Console.WriteLine("Connected to client: " + data.GetProperty("rorshKey").GetString());
                        break;

                    case "disconnected":
                        Console.WriteLine("Disconnected from client: " + data.GetProperty("rorshKey").GetString());
                        _currentClientKey = null;
                        break;

                    case "output":
                        Console.Write(data.GetProperty("output").GetString());
                        break;

                    case "error_output":
                        Console.WriteLine("ERROR: " + data.GetProperty("error").GetString());
                        break;

                    case "command_exit":
                        Console.WriteLine("\n[Command exited with code: " + data.GetProperty("code").GetInt32() + "]");
                        break;

                    case "error":
                        Console.WriteLine("Server error: " + data.GetProperty("message").GetString());
                        break;

                    case "session_end":
                        Console.WriteLine("Session ended by server.");
                        _currentClientKey = null;
                        break;
                }
            }
        }

        static string Encrypt(string plainText)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = KeyBuffer;
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

        static string Decrypt(string cipherText)
        {
            byte[] fullBytes = Convert.FromHexString(cipherText);
            byte[] iv = new byte[12]; // GCM standard IV
            byte[] cipherBytes = new byte[fullBytes.Length - 12];

            Buffer.BlockCopy(fullBytes, 0, iv, 0, 12);
            Buffer.BlockCopy(fullBytes, 12, cipherBytes, 0, cipherBytes.Length);

            using (var aes = Aes.Create())
            {
                aes.Key = KeyBuffer;
                aes.IV = iv;
                aes.Mode = CipherMode.GCM;
                aes.Padding = PaddingMode.None;

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
        }

        static string ReadPassword()
        {
            StringBuilder password = new StringBuilder();
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
                {
                    password.Append(key.KeyChar);
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                    Console.Write("\b \b");
                }
            } while (key.Key != ConsoleKey.Enter);
            return password.ToString();
        }
    }
}
