// RORSH-Gate List Cloud Files Command

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using RorshGate.Core;

namespace RorshGate.Commands
{
    public class ListCloudCommand
    {
        private readonly WssClient _client;
        private readonly TaskCompletionSource<bool> _tcs;

        public ListCloudCommand(WssClient client)
        {
            _client = client;
            _tcs = new TaskCompletionSource<bool>();

            _client.OnMessageReceived += (s, msg) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(msg);
                    var root = doc.RootElement;

                    if (root.GetProperty("command").GetString() == "get-list-cloud")
                    {
                        if (root.GetProperty("type").GetString() == "response")
                        {
                            var files = root.GetProperty("data");
                            Console.WriteLine("");
                            Console.WriteLine("Files on Cloud Server:");
                            Console.WriteLine("======================");
                            Console.WriteLine("");

                            if (files.GetArrayLength() == 0)
                            {
                                Console.WriteLine("  No files available.");
                            }
                            else
                            {
                                Console.WriteLine($"  {'Name',-40} {'Size',-15} {'SHA-256',-64}");
                                Console.WriteLine($"  {new string('-', 40)} {new string('-', 15)} {new string('-', 64)}");

                                foreach (var file in files.EnumerateArray())
                                {
                                    string name = file.GetProperty("name").GetString() ?? "unknown";
                                    long size = file.GetProperty("size").GetInt64();
                                    string sha256 = file.GetProperty("sha256").GetString() ?? "N/A";
                                    string sizeStr = FormatBytes(size);

                                    Console.WriteLine($"  {name,-40} {sizeStr,-15} {sha256,-64}");
                                }
                            }

                            Console.WriteLine("");
                            _tcs.TrySetResult(true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error parsing list-cloud response: {ex.Message}");
                    _tcs.TrySetResult(false);
                }
            };
        }

        public async Task<bool> ExecuteAsync()
        {
            await _client.SendCommandAsync("get-list-cloud");
            return await _tcs.Task;
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
