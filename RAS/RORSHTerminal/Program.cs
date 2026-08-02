using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RORSHTerminal
{
    /// <summary>
    /// RORSH Admin Shell (RAS)
    /// Terminal-based admin interface for managing RORSH clients
    /// 
    /// Features:
    /// - Native terminal interface (no separate GUI)
    /// - Connects to SecureCom server via WSS
    /// - Lists all connected clients
    /// - Establishes SSH-like sessions with clients
    /// - Real-time command execution and output streaming
    /// </summary>
    class Program
    {
        // Hardcoded server URL
        private const string ServerUrl = "wss://rorsh-openweb-ssh.onrender.com";
        private static WssClient _client;
        private static CommandHandler _handler;
        private static bool _isRunning = true;

        static async Task Main(string[] args)
        {
            Console.WriteLine(@"
    ____  ____  ____  _   _         
   |  _ \|  _ \|  _ \| | | |  /\   
   | |_) | |_) | |_) | |_| | /  \  
   |  _ <|  _ <|  _ <|  _  |/ /\ \ 
   | | \ \ | \ \ | \ \ | | | / ____ \
   |_|  \_\_|  \_\_|  \_\_| /_/    \_\

   RORSH Admin Shell (RAS) v1.0
   Secure Remote Administration Terminal
   =====================================
");

            _client = new WssClient(ServerUrl);
            _handler = new CommandHandler(_client);

            // Handle graceful shutdown
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                _isRunning = false;
            };

            // Main command loop
            while (_isRunning)
            {
                try
                {
                    Console.Write("RAS> ");
                    var input = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    // Special handling for Start-RAS
                    if (input.Trim().Equals("Start-RAS", StringComparison.OrdinalIgnoreCase))
                    {
                        await StartRas();
                        continue;
                    }

                    // Process other commands
                    var shouldContinue = await _handler.ProcessCommand(input);
                    if (!shouldContinue)
                    {
                        _isRunning = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] {ex.Message}");
                }
            }

            Console.WriteLine("[RAS] Goodbye.");
        }

        /// <summary>
        /// Start RAS - connect and authenticate to server
        /// </summary>
        static async Task StartRas()
        {
            if (_client.IsConnected)
            {
                Console.WriteLine("[RAS] Already connected.");
                return;
            }

            Console.Write("Admin ID: ");
            var adminId = Console.ReadLine();

            Console.Write("Password: ");
            var password = ReadPassword();
            Console.WriteLine();

            if (string.IsNullOrWhiteSpace(adminId) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("[Error] Admin ID and password are required.");
                return;
            }

            Console.WriteLine($"[RAS] Connecting to {ServerUrl}...");

            var success = await _client.ConnectAsync(adminId, password);

            if (success)
            {
                Console.WriteLine("[RAS] Connected and authenticated successfully.");
                Console.WriteLine("[RAS] Type 'c-list' to see connected clients.");
                Console.WriteLine("[RAS] Type 'help' for command reference.");
            }
            else
            {
                Console.WriteLine("[RAS] Connection or authentication failed.");
            }
        }

        /// <summary>
        /// Read password without echoing characters
        /// </summary>
        static string ReadPassword()
        {
            var password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            } while (key.Key != ConsoleKey.Enter);

            return password;
        }
    }
}
