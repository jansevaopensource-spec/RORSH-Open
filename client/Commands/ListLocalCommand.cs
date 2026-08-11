// RORSH-Gate List Local Files Command

using System;
using System.IO;
using System.Linq;
using RorshGate.Core;

namespace RorshGate.Commands
{
    public static class ListLocalCommand
    {
        public static void Execute()
        {
            var files = FileManager.ListLocalFiles();

            Console.WriteLine("");
            Console.WriteLine("Local Downloaded Files:");
            Console.WriteLine("========================");
            Console.WriteLine($"Location: {Config.DownloadsDirectory}");
            Console.WriteLine("");

            if (files.Count == 0)
            {
                Console.WriteLine("  No files downloaded yet.");
                Console.WriteLine("  Use get-cloud-down <filename> to download files.");
            }
            else
            {
                Console.WriteLine($"  {"Name",-40} {"Size",-15}");
                Console.WriteLine($"  {new string('-', 40)} {new string('-', 15)}");

                foreach (var file in files)
                {
                    string path = FileManager.GetLocalFilePath(file);
                    long size = new FileInfo(path).Length;
                    string sizeStr = FormatBytes(size);
                    Console.WriteLine($"  {file,-40} {sizeStr,-15}");
                }

                Console.WriteLine("");
                Console.WriteLine($"  Total files: {files.Count}");
            }

            Console.WriteLine("");
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }
}
