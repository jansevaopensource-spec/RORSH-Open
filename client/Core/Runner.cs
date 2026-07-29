// RORSH-Gate File Runner
// Executes downloaded files based on platform and extension

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace RorshGate.Core
{
    public static class Runner
    {
        // Windows executable extensions
        private static readonly string[] WindowsExecExtensions = new[]
        {
            ".exe", ".msi", ".msix", ".appx", ".appxbundle", ".msixbundle",
            ".bat", ".cmd", ".com", ".ps1", ".vbs", ".js", ".jse",
            ".wsf", ".wsh", ".msc", ".scr", ".cpl", ".lnk",
            ".jar", ".py", ".pyw", ".psm1", ".psd1", ".reg", ".hta"
        };

        // Linux executable extensions
        private static readonly string[] LinuxExecExtensions = new[]
        {
            ".run", ".bin", ".sh", ".elf", ".out", ".AppImage",
            ".deb", ".rpm", ".pkg.tar.zst", ".snap", ".flatpak"
        };

        // Common document/media extensions (open with default app)
        private static readonly string[] CommonDocExtensions = new[]
        {
            ".mp3", ".mp4", ".avi", ".mkv", ".mov", ".wmv",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm",
            ".zip", ".rar", ".7z", ".tar", ".gz"
        };

        public static bool RunFile(string filename)
        {
            string filePath = FileManager.GetLocalFilePath(filename);

            if (!File.Exists(filePath))
            {
                Logger.Error($"File not found locally: {filename}");
                Console.WriteLine("Error: File not found locally. Use get-cloud-down first.");
                return false;
            }

            string extension = Path.GetExtension(filename).ToLowerInvariant();

            try
            {
                if (Config.IsWindows)
                {
                    return RunOnWindows(filePath, extension);
                }
                else if (Config.IsLinux)
                {
                    return RunOnLinux(filePath, extension);
                }
                else
                {
                    Logger.Error("Unsupported platform");
                    Console.WriteLine("Error: Unsupported platform.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Execution failed: {ex.Message}");
                Console.WriteLine($"Error: Execution failed - {ex.Message}");
                return false;
            }
        }

        private static bool RunOnWindows(string filePath, string extension)
        {
            // Check if it is an executable
            if (Array.Exists(WindowsExecExtensions, e => e == extension))
            {
                Logger.Info($"Executing on Windows: {filePath}");

                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(filePath) ?? string.Empty
                };

                Process.Start(psi);
                Console.WriteLine($"Started: {Path.GetFileName(filePath)}");
                return true;
            }

            // Check if it is a common document
            if (Array.Exists(CommonDocExtensions, e => e == extension))
            {
                Logger.Info($"Opening document: {filePath}");

                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                };

                Process.Start(psi);
                Console.WriteLine($"Opened: {Path.GetFileName(filePath)}");
                return true;
            }

            // Unknown extension - try shell execute anyway
            Logger.Warn($"Unknown extension, attempting shell execute: {extension}");
            var fallbackPsi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            Process.Start(fallbackPsi);
            Console.WriteLine($"Attempted to open: {Path.GetFileName(filePath)}");
            return true;
        }

        private static bool RunOnLinux(string filePath, string extension)
        {
            // Check if it is an executable
            if (Array.Exists(LinuxExecExtensions, e => e == extension))
            {
                Logger.Info($"Executing on Linux: {filePath}");

                // Make executable if needed
                if (extension == ".sh" || extension == ".bin" || extension == ".run" || 
                    extension == ".elf" || extension == ".out" || extension == ".AppImage")
                {
                    try
                    {
                        var chmodPsi = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = "+x "" + filePath + """,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        using var chmodProcess = Process.Start(chmodPsi);
                        chmodProcess?.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"chmod failed (non-critical): {ex.Message}");
                    }
                }

                // Execute
                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(filePath) ?? string.Empty
                };

                Process.Start(psi);
                Console.WriteLine($"Started: {Path.GetFileName(filePath)}");
                return true;
            }

            // Check if it is a common document
            if (Array.Exists(CommonDocExtensions, e => e == extension))
            {
                Logger.Info($"Opening document on Linux: {filePath}");

                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = """ + filePath + """,
                    UseShellExecute = false
                };

                Process.Start(psi);
                Console.WriteLine($"Opened: {Path.GetFileName(filePath)}");
                return true;
            }

            // Unknown extension - try xdg-open
            Logger.Warn($"Unknown extension, attempting xdg-open: {extension}");
            var fallbackPsi = new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = """ + filePath + """,
                UseShellExecute = false
            };
            Process.Start(fallbackPsi);
            Console.WriteLine($"Attempted to open: {Path.GetFileName(filePath)}");
            return true;
        }
    }
}
