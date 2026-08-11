// RORSH-Gate Configuration
// Hardcoded server URL and paths

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RorshGate.Core
{
    public static class Config
    {
        // Hardcoded server URL - replace with actual Render URL
        public const string SERVER_URL = "wss://UWOEBEUSYSBSNSOS8HSBSBSJSHZUZ72N00S8SUBSHSHGSHHKOQPBXT7S62B2862Y2B2J6282927.onrender.com";
        public const string SERVER_HTTP_URL = "https://UWOEBEUSYSBSNSOS8HSBSBSJSHZUZ72N00S8SUBSHSHGSHHKOQPBXT7S62B2862Y2B2J6282927.onrender.com";

        // Local directories
        public static string BaseDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RORSH-Gate"
        );

        public static string DownloadsDirectory => Path.Combine(BaseDirectory, "downloads");
        public static string LogsDirectory => Path.Combine(BaseDirectory, "logs");

        // Platform detection
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        // Ensure directories exist
        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(BaseDirectory);
            Directory.CreateDirectory(DownloadsDirectory);
            Directory.CreateDirectory(LogsDirectory);
        }

        // Log file path
        public static string LogFilePath => Path.Combine(
            LogsDirectory,
            $"rorsh-gate-{DateTime.Now:yyyyMMdd}.log"
        );
    }
}
