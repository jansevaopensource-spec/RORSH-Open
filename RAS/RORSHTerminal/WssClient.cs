using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RORSHTerminal
{
    /// <summary>
    /// WebSocket Secure client for connecting to RORSH SecureCom server
    /// Handles admin authentication, client listing, and session management
    /// </summary>
    public class WssClient
    {
        private readonly string _serverUrl;
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private bool _isConnected = false;
        private bool _isAuthenticated = false;
        private string _encryptionKey = null;
        private string _currentSessionKey = null;

        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;
        public event EventHandler<string> OnError;
        public event EventHandler<string> OnOutput;
        public event EventHandler OnClientListUpdated;
        public event EventHandler OnSessionStarted;
        public event EventHandler OnSessionEnded;

        public bool IsConnected => _isConnected;
        public bool IsAuthenticated => _isAuthenticated;

        public WssClient(string serverUrl)
        {
            _serverUrl = serverUrl;
        }

        /// <summary>
        /// Connect and authenticate as admin
        /// </summary>
        public async Task<bool> ConnectAsync(string adminId, string password)
        {
            try
            {
                _cts = new CancellationTokenSource();
                _ws = new ClientWebSocket();

                _ws.Options.SetRequestHeader("X-Client-Type", "rorsh-admin");

                var uri = new Uri(_serverUrl);
                await _ws.ConnectAsync(uri, _cts.Token);

                _isConnected = true;
                Console.WriteLine($"[WSS] Connected to {_serverUrl}");

                // Generate key pair and authenticate
                var (privateKey, publicKey) = Crypto.GenerateKeyPair();
                _encryptionKey = Crypto.DeriveKey(privateKey + publicKey);

                var authPayload = new
                {
                    adminId = adminId,
                    password = password,
                    publicKey = publicKey
                };

                await SendMessageAsync("admin_auth", authPayload);

                // Start receive loop
                _receiveTask = Task.Run(ReceiveLoop);

                // Wait for auth response
                var authTimeout = DateTime.Now.AddSeconds(10);
                while (!_isAuthenticated && DateTime.Now < authTimeout)
                {
                    await Task.Delay(100);
                }

                if (_isAuthenticated)
                {
                    OnConnected?.Invoke(this, EventArgs.Empty);
                    return true;
                }
                else
                {
                    Console.WriteLine("[WSS] Authentication timeout");
                    await DisconnectAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WSS] Connection error: {ex.Message}");
                OnError?.Invoke(this, ex.Message);
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Main receive loop
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
        /// Process incoming messages
        /// </summary>
        private void ProcessMessage(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                switch (type)
                {
                    case "auth_success":
                        _isAuthenticated = true;
                        Console.WriteLine("[WSS] Authentication successful");
                        break;

                    case "auth_failed":
                        _isAuthenticated = false;
                        Console.WriteLine("[WSS] Authentication failed");
                        break;

                    case "client_list":
                        Console.WriteLine("\n[Clients] Connected clients:");
                        Console.WriteLine("{0,-12} {1,-20} {2,-16} {3,-10} {4,-20}", 
                            "RorshKey", "Hostname", "IP Address", "OS", "Status");
                        Console.WriteLine(new string('-', 80));

                        if (doc.RootElement.TryGetProperty("payload", out var listPayload))
                        {
                            if (listPayload.ValueKind == JsonValueKind.Object && 
                                listPayload.TryGetProperty("clients", out var clients))
                            {
                                if (clients.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var client in clients.EnumerateArray())
                                    {
                                        try
                                        {
                                            var key = client.GetProperty("rorshKey").GetString() ?? "N/A";
                                            var host = client.GetProperty("hostname").GetString() ?? "N/A";
                                            var ip = client.GetProperty("ip").GetString() ?? "N/A";
                                            var osName = client.GetProperty("os").GetString() ?? "N/A";
                                            var status = client.GetProperty("status").GetString() ?? "N/A";
                                            Console.WriteLine("{0,-12} {1,-20} {2,-16} {3,-10} {4,-20}", 
                                                key, host, ip, osName, status);
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"[WARN] Malformed client entry: {ex.Message}");
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("  No clients connected.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("  No clients connected.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("  No clients connected.");
                        }
                        Console.WriteLine();
                        OnClientListUpdated?.Invoke(this, EventArgs.Empty);
                        break;

                    case "session_started":
                        var sessPayload = doc.RootElement.GetProperty("payload");
                        _currentSessionKey = sessPayload.GetProperty("sessionKey").GetString();
                        var clientKey = sessPayload.GetProperty("rorshKey").GetString();
                        Console.WriteLine($"[WSS] Session started with client: {clientKey}");
                        OnSessionStarted?.Invoke(this, EventArgs.Empty);
                        break;

                    case "session_ended":
                        _currentSessionKey = null;
                        Console.WriteLine("[WSS] Session ended");
                        OnSessionEnded?.Invoke(this, EventArgs.Empty);
                        break;

                    case "cmd_output":
                        if (doc.RootElement.TryGetProperty("payload", out var outPayload))
                        {
                            if (outPayload.ValueKind == JsonValueKind.Object && 
                                outPayload.TryGetProperty("output", out var outputElem))
                            {
                                var output = outputElem.GetString() ?? "";
                                Console.Write(output);
                                OnOutput?.Invoke(this, output);
                            }
                        }
                        break;

                    case "error":
                        if (doc.RootElement.TryGetProperty("payload", out var errPayload))
                        {
                            if (errPayload.ValueKind == JsonValueKind.Object && 
                                errPayload.TryGetProperty("message", out var msgElem))
                            {
                                var errMsg = msgElem.GetString() ?? "Unknown error";
                                Console.WriteLine($"[Error] {errMsg}");
                            }
                        }
                        break;

                    default:
                        Console.WriteLine($"[WSS] Unknown message type: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WSS] Message processing error: {ex.Message}");
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
        /// Request client list
        /// </summary>
        public async Task ListClients()
        {
            await SendMessageAsync("list_clients", new { });
        }

        /// <summary>
        /// Connect to a client by RorshKey
        /// </summary>
        public async Task ConnectClient(string rorshKey)
        {
            await SendMessageAsync("connect_client", new { rorshKey = rorshKey });
        }

        /// <summary>
        /// Disconnect from current client
        /// </summary>
        public async Task DisconnectClient(string rorshKey)
        {
            await SendMessageAsync("disconnect_client", new { rorshKey = rorshKey });
        }

        /// <summary>
        /// Send command to connected client
        /// </summary>
        public async Task SendCommand(string rorshKey, string command)
        {
            await SendMessageAsync("admin_command", new { rorshKey = rorshKey, command = command });
        }

        /// <summary>
        /// Send shell resize event
        /// </summary>
        public async Task SendShellResize(string rorshKey, int cols, int rows)
        {
            await SendMessageAsync("shell_resize", new { rorshKey = rorshKey, cols = cols, rows = rows });
        }

        /// <summary>
        /// Handle disconnection
        /// </summary>
        private async Task HandleDisconnect()
        {
            _isConnected = false;
            _isAuthenticated = false;
            _currentSessionKey = null;

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
    }
}
