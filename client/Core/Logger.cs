// RORSH-Gate Logger
// Logs to both console and file

using System;
using System.IO;
using System.Threading;

namespace RorshGate.Core
{
    public static class Logger
    {
        private static readonly object LockObj = new object();

        public static void Info(string message)
        {
            Log("INFO", message);
        }

        public static void Error(string message)
        {
            Log("ERROR", message);
        }

        public static void Warn(string message)
        {
            Log("WARN", message);
        }

        public static void Debug(string message)
        {
            Log("DEBUG", message);
        }

        private static void Log(string level, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string entry = $"[{timestamp}] [{level}] {message}";

            // Console output
            Console.WriteLine(entry);

            // File output
            try
            {
                lock (LockObj)
                {
                    File.AppendAllText(Config.LogFilePath, entry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER ERROR] Failed to write to log file: {ex.Message}");
            }
        }
    }
}
