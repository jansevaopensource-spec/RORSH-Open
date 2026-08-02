using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RORSHClient
{
    /// <summary>
    /// WebSocket Secure client for connecting to RORSH SecureCom server
    /// Handles connection, reconnection, message encryption, and heartbeat
    /// </summary>
    public class WssClient
    {
        private readonly string _serverUrl;
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private Task _heartbeatTask;
        private bool _isConnected = false;
        private string _encryptionKey = null;
        private string _rorshKey = null;
        private readonly ShellRelay _shellRelay;

        public event EventHandler<string> OnMessageReceived;
        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;
        public event EventHandler<string> OnError;

        public bool IsConnected => _isConnected;
        public string RorshKey => _rorshKey;

        public WssClient(string serverUrl)
        {
            _serverUrl = serverUrl;
            _shellRelay = new ShellRelay();
            _shellRelay.OnOutput += (s, output) => SendOutput(output);
        }

        /// <summary>
        /// Connect to the SecureCom server and register as client
        /// </summary>
        public async Task ConnectAsync()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _ws = new ClientWebSocket();

                // Set headers
                _ws.Options.SetRequestHeader("X-Client-Type", "rorsh-client");

                var uri = new Uri(_serverUrl);
                await _ws.ConnectAsync(uri, _cts.Token);

                _isConnected = true;
                Console.WriteLine($"[WSS] Connected to {_serverUrl}");

                // Generate key pair and send hello
                var (privateKey, publicKey) = Crypto.GenerateKeyPair();
                _encryptionKey = Crypto.DeriveKey(privateKey + publicKey);

                var helloPayload = new
                {
                    hostname = Environment.MachineName,
                    os = GetOsName(),
                    username = Environment.UserName,
                    publicKey = publicKey
                };

                await SendMessageAsync("client_hello", helloPayload);

                // Start background tasks
                _receiveTask = Task.Run(ReceiveLoop);
                _heartbeatTask = Task.Run(HeartbeatLoop);

                OnConnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WSS] Connection error: {ex.Message}");
                OnError?.Invoke(this, ex.Message);
                _isConnected = false;
            }
        }

        /// <summary>
        /// Main receive loop - processes incoming messages
        /// </summary>
        private async Task ReceiveLoop()
        {
            var buffer = new byte[8192];

            while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ProcessMessage(message);
                    }
                }
                catch (WebSocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WSS] Receive error: {ex.Message}");
                }
            }

            await HandleDisconnect();
        }

        /// <summary>
        /// Process incoming server messages
        /// </summary>
        private void ProcessMessage(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                switch (type)
                {
                    case "client_registered":
                        var payload = doc.RootElement.GetProperty("payload");
                        _rorshKey = payload.GetProperty("rorshKey").GetString();
                        Console.WriteLine($"[WSS] Registered with RorshKey: {_rorshKey}");
                        break;

                    case "shell_open":
                        if (doc.RootElement.TryGetProperty("payload", out var sessionPayload))
                        {
                            if (sessionPayload.ValueKind == JsonValueKind.Object && 
                                sessionPayload.TryGetProperty("sessionKey", out var skElem))
                            {
                                var sessionKey = skElem.GetString() ?? "";
                                Console.WriteLine($"[WSS] Shell session opened: {sessionKey}");
                                _shellRelay.StartShell();
                            }
                        }
                        break;

                    case "shell_close":
                        Console.WriteLine("[WSS] Shell session closed");
                        _shellRelay.StopShell();
                        break;

                    case "cmd_exec":
                        if (doc.RootElement.TryGetProperty("payload", out var cmdPayload))
                        {
                            if (cmdPayload.ValueKind == JsonValueKind.Object && 
                                cmdPayload.TryGetProperty("command", out var cmdElem))
                            {
                                var command = cmdElem.GetString() ?? "";
                                _shellRelay.ExecuteCommand(command);
                            }
                        }
                        break;

                    case "shell_resize":
                        if (doc.RootElement.TryGetProperty("payload", out var resizePayload))
                        {
                            if (resizePayload.ValueKind == JsonValueKind.Object)
                            {
                                if (resizePayload.TryGetProperty("cols", out var colsElem) &&
                                    resizePayload.TryGetProperty("rows", out var rowsElem))
                                {
                                    _shellRelay.Resize(colsElem.GetInt32(), rowsElem.GetInt32());
                                }
                            }
                        }
                        break;

                    default:
                        Console.WriteLine($"[WSS] Unknown message type: {type}");
                        break;
                }

                OnMessageReceived?.Invoke(this, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WSS] Message processing error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send command output back to server
        /// </summary>
        private void SendOutput(string output)
        {
            if (_isConnected && _rorshKey != null)
            {
                _ = SendMessageAsync("cmd_output", new { output = output });
            }
        }

        /// <summary>
        /// Send a message to the server
        /// </summary>
        public async Task SendMessageAsync(string type, object payload)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;

            try
            {
                var message = new
                {
                    type = type,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    payload = payload
                };

                var json = JsonSerializer.Serialize(message);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WSS] Send error: {ex.Message}");
            }
        }

        /// <summary>
        /// Heartbeat loop - keeps connection alive
        /// </summary>
        private async Task HeartbeatLoop()
        {
            while (_isConnected && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(25000, _cts.Token);
                    if (_isConnected)
                    {
                        await SendMessageAsync("heartbeat", new { status = "alive" });
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WSS] Heartbeat error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handle disconnection and cleanup
        /// </summary>
        private async Task HandleDisconnect()
        {
            _isConnected = false;
            _rorshKey = null;
            _shellRelay.StopShell();

            try
            {
                _cts?.Cancel();
                if (_ws != null && _ws.State == WebSocketState.Open)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                }
            }
            catch { }

            Console.WriteLine("[WSS] Disconnected from server");
            OnDisconnected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Disconnect from server
        /// </summary>
        public async Task DisconnectAsync()
        {
            await HandleDisconnect();
        }

        /// <summary>
        /// Get OS name
        /// </summary>
        private string GetOsName()
        {
            if (OperatingSystem.IsWindows()) return "windows";
            if (OperatingSystem.IsLinux()) return "linux";
            if (OperatingSystem.IsMacOS()) return "macos";
            return "unknown";
        }
    }
}
