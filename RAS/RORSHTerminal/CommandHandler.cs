using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RORSHTerminal
{
    /// <summary>
    /// Command handler for RAS terminal interface
    /// Parses admin commands and routes them to the WSS client
    /// </summary>
    public class CommandHandler
    {
        private readonly WssClient _client;
        private string _currentClientKey = null;
        private bool _isInSession = false;

        public CommandHandler(WssClient client)
        {
            _client = client;
            _client.OnSessionStarted += (s, e) => { _isInSession = true; };
            _client.OnSessionEnded += (s, e) => { _isInSession = false; _currentClientKey = null; };
        }

        /// <summary>
        /// Process a command from the admin
        /// </summary>
        public async Task<bool> ProcessCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return true;

            var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLower();

            switch (command)
            {
                case "help":
                    ShowHelp();
                    return true;

                case "exit":
                case "quit":
                    Console.WriteLine("[RAS] Disconnecting...");
                    if (_isInSession && _currentClientKey != null)
                    {
                        await _client.DisconnectClient(_currentClientKey);
                    }
                    await _client.DisconnectAsync();
                    return false; // Signal to exit main loop

                case "c-list":
                    if (!_client.IsAuthenticated)
                    {
                        Console.WriteLine("[Error] Not authenticated. Use Start-RAS first.");
                        return true;
                    }
                    await _client.ListClients();
                    return true;

                case "get-connect":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("[Error] Usage: get-connect @<rorshkey>");
                        return true;
                    }
                    if (!_client.IsAuthenticated)
                    {
                        Console.WriteLine("[Error] Not authenticated. Use Start-RAS first.");
                        return true;
                    }
                    var key = parts[1].Replace("@", "");
                    _currentClientKey = key;
                    await _client.ConnectClient(key);
                    Console.WriteLine($"[RAS] Connecting to client {key}...");
                    return true;

                case "get-disconnect":
                    if (!_isInSession || _currentClientKey == null)
                    {
                        Console.WriteLine("[Error] No active session. Use get-connect first.");
                        return true;
                    }
                    await _client.DisconnectClient(_currentClientKey);
                    Console.WriteLine("[RAS] Disconnecting from client...");
                    return true;

                case "clear":
                    Console.Clear();
                    return true;

                case "status":
                    Console.WriteLine($"[Status] Connected: {_client.IsConnected}");
                    Console.WriteLine($"[Status] Authenticated: {_client.IsAuthenticated}");
                    Console.WriteLine($"[Status] In Session: {_isInSession}");
                    if (_currentClientKey != null)
                    {
                        Console.WriteLine($"[Status] Current Client: {_currentClientKey}");
                    }
                    return true;

                default:
                    // If in session, treat as command to send to client
                    if (_isInSession && _currentClientKey != null)
                    {
                        await _client.SendCommand(_currentClientKey, input);
                        return true;
                    }

                    Console.WriteLine($"[Error] Unknown command: {command}");
                    Console.WriteLine("[Info] Type 'help' for available commands");
                    return true;
            }
        }

        /// <summary>
        /// Show help information
        /// </summary>
        private void ShowHelp()
        {
            Console.WriteLine(@"
RORSH Admin Shell (RAS) - Command Reference
===========================================

Connection Commands:
  Start-RAS          Connect and authenticate to SecureCom server
  exit / quit        Disconnect and exit RAS

Client Management:
  c-list             List all connected clients with RorshKey, hostname, IP
  get-connect @key   Connect to a client by RorshKey (e.g., get-connect @1234567890)
  get-disconnect     Disconnect from current client session

Session Commands (when connected to client):
  <any command>      Execute command on the client's shell
  Ctrl+C             Send interrupt signal (type 'exit' to end session)

Utility Commands:
  help               Show this help message
  clear              Clear the terminal screen
  status             Show connection status

Notes:
- All connections use WSS (WebSocket Secure) with TLS
- Payloads are encrypted with AES-256-GCM end-to-end
- RorshKey is a 10-digit identifier regenerated on each client reconnect
");
        }

        /// <summary>
        /// Check if currently in a session
        /// </summary>
        public bool IsInSession => _isInSession;
    }
}
