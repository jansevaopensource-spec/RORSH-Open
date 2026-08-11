// RORSH-Gate CLI Application
// Entry point and command loop

using System;
using System.Text.Json;
using System.Threading.Tasks;
using RorshGate.Assets;
using RorshGate.Commands;
using RorshGate.Core;

namespace RorshGate
{
    class Program
    {
        private static WssClient? _client;
        private static bool _isRunning = false;

        static async Task Main(string[] args)
        {
            // Ensure directories exist
            Config.EnsureDirectories();

            Logger.Info("RORSH-Gate Client Starting...");

            // Check if get-serve was called
            if (args.Length == 0 || args[0] != "get-serve")
            {
                Console.WriteLine("");
                Console.WriteLine("Welcome to RORSH-Gate");
                Console.WriteLine("=====================");
                Console.WriteLine("");
                Console.WriteLine("To start, run: rorsh-gate get-serve");
                Console.WriteLine("");
                return;
            }

            // Display banner
            AsciiArt.PrintBanner();

            // Get system info
            string hostname = SystemInfo.GetHostname();
            string ipv4 = SystemInfo.GetIPv4Address();

            Logger.Info($"Hostname: {hostname}");
            Logger.Info($"IPv4: {ipv4}");
            Logger.Info($"Platform: {(Config.IsWindows ? "Windows" : (Config.IsLinux ? "Linux" : "Unknown"))}");

            // Connect to server
            Console.WriteLine($"Connecting to server: {Config.SERVER_URL}");
            Console.WriteLine($"Identity: {hostname} ({ipv4})");
            Console.WriteLine("");

            _client = new WssClient(Config.SERVER_URL);

            // Handle server messages
            _client.OnMessageReceived += (s, msg) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(msg);
                    var root = doc.RootElement;
                    string type = root.GetProperty("type").GetString() ?? "unknown";

                    if (type == "connected")
                    {
                        string serverMsg = root.GetProperty("message").GetString() ?? "Connected";
                        Console.WriteLine($"[Server] {serverMsg}");
                    }
                    else if (type == "error")
                    {
                        string errorMsg = root.GetProperty("message").GetString() ?? "Unknown error";
                        Console.WriteLine($"[Error] {errorMsg}");
                    }
                }
                catch
                {
                    // Not a standard message, ignore
                }
            };

            _client.OnError += (s, err) =>
            {
                Console.WriteLine($"[Connection Error] {err}");
            };

            _client.OnDisconnected += (s, e) =>
            {
                Console.WriteLine("[Disconnected from server]");
                _isRunning = false;
            };

            bool connected = await _client.ConnectAsync(hostname, ipv4);

            if (!connected)
            {
                Console.WriteLine("Failed to connect to server. Check your network and try again.");
                return;
            }

            Console.WriteLine("Connected successfully!");
            Console.WriteLine("Type 'get-help' for available commands.");
            Console.WriteLine("");

            // Command loop
            _isRunning = true;
            while (_isRunning)
            {
                Console.Write("rorsh-gate> ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                string[] parts = input.Trim().Split(' ', 2);
                string command = parts[0].ToLowerInvariant();
                string? argument = parts.Length > 1 ? parts[1] : null;

                Logger.Info($"User command: {command} {argument ?? ""}");

                switch (command)
                {
                    case "get-help":
                        HelpCommand.Execute();
                        break;

                    case "get-list-cloud":
                        if (_client.IsConnected)
                        {
                            var listCloud = new ListCloudCommand(_client);
                            await listCloud.ExecuteAsync();
                        }
                        else
                        {
                            Console.WriteLine("Not connected to server.");
                        }
                        break;

                    case "get-list-local":
                        ListLocalCommand.Execute();
                        break;

                    case "get-cloud-down":
                        if (_client.IsConnected)
                        {
                            if (string.IsNullOrWhiteSpace(argument))
                            {
                                Console.WriteLine("Usage: get-cloud-down <filename> or get-cloud-down all");
                            }
                            else
                            {
                                var download = new DownloadCommand(_client);
                                await download.ExecuteAsync(argument);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Not connected to server.");
                        }
                        break;

                    case "get-run":
                        if (string.IsNullOrWhiteSpace(argument))
                        {
                            Console.WriteLine("Usage: get-run <filename>");
                        }
                        else
                        {
                            RunCommand.Execute(argument);
                        }
                        break;

                    case "get-end":
                        if (_client.IsConnected)
                        {
                            var endCmd = new EndCommand(_client);
                            await endCmd.ExecuteAsync();
                        }
                        _isRunning = false;
                        break;

                    case "get-serve":
                        Console.WriteLine("Already connected.");
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        Console.WriteLine("Type 'get-help' for available commands.");
                        break;
                }
            }

            // Cleanup
            if (_client != null)
            {
                await _client.DisconnectAsync();
                _client.Dispose();
            }

            Logger.Info("RORSH-Gate Client Exiting...");
            Console.WriteLine("Goodbye!");
        }
    }
}
