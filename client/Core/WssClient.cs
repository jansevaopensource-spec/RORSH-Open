// RORSH-Gate WebSocket Secure Client
// Enforces TLS 1.2, handles live server connection

using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RorshGate.Core
{
    public class WssClient : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private readonly Uri _serverUri;
        private bool _isConnected;
        private readonly CancellationTokenSource _cts;

        public bool IsConnected => _isConnected;
        public event EventHandler<string>? OnMessageReceived;
        public event EventHandler<string>? OnError;
        public event EventHandler? OnDisconnected;

        public WssClient(string serverUrl)
        {
            _serverUri = new Uri(serverUrl);
            _cts = new CancellationTokenSource();
            _isConnected = false;
        }

        public async Task<bool> ConnectAsync(string hostname, string ipv4)
        {
            try
            {
                // Enforce TLS 1.2
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                _webSocket = new ClientWebSocket();

                // Set headers for identification
                _webSocket.Options.SetRequestHeader("X-Client-Hostname", hostname);
                _webSocket.Options.SetRequestHeader("X-Client-IP", ipv4);

                // Connect with timeout
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

                await _webSocket.ConnectAsync(_serverUri, linkedCts.Token);
                _isConnected = true;

                Logger.Info($"Connected to server: {_serverUri}");

                // Start listening for messages
                _ = Task.Run(async () => await ReceiveLoopAsync(), _cts.Token);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Connection failed: {ex.Message}");
                OnError?.Invoke(this, ex.Message);
                return false;
            }
        }

        public async Task SendCommandAsync(string command, string? args = null)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                Logger.Error("Cannot send command: not connected");
                return;
            }

            var message = new
            {
                command = command,
                args = args ?? string.Empty
            };

            string json = JsonSerializer.Serialize(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token
            );

            Logger.Info($"Sent command: {command} {args}");
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[8192];

            try
            {
                while (_webSocket != null && _webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), 
                        _cts.Token
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _isConnected = false;
                        OnDisconnected?.Invoke(this, EventArgs.Empty);
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    OnMessageReceived?.Invoke(this, message);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Receive loop cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error($"Receive error: {ex.Message}");
                OnError?.Invoke(this, ex.Message);
            }
            finally
            {
                _isConnected = false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client disconnecting",
                        CancellationToken.None
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Disconnect error: {ex.Message}");
            }
            finally
            {
                _isConnected = false;
                _cts.Cancel();
                _webSocket?.Dispose();
                _webSocket = null;
            }
        }

        public void Dispose()
        {
            _ = DisconnectAsync();
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
