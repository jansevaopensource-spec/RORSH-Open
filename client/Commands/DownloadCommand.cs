// RORSH-Gate Download Command

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using RorshGate.Core;

namespace RorshGate.Commands
{
    public class DownloadCommand
    {
        private readonly WssClient _client;
        private readonly TaskCompletionSource<bool> _tcs;
        private bool _isAll;

        public DownloadCommand(WssClient client)
        {
            _client = client;
            _tcs = new TaskCompletionSource<bool>();
            _isAll = false;

            _client.OnMessageReceived += async (s, msg) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(msg);
                    var root = doc.RootElement;

                    if (root.GetProperty("command").GetString() == "get-cloud-down")
                    {
                        if (root.GetProperty("type").GetString() == "error")
                        {
                            string errorMsg = root.GetProperty("message").GetString() ?? "Unknown error";
                            Console.WriteLine($"Error: {errorMsg}");
                            _tcs.TrySetResult(false);
                            return;
                        }

                        if (_isAll)
                        {
                            // Download all files
                            var files = root.GetProperty("data");
                            var fileList = new List<ManifestFile>();

                            foreach (var file in files.EnumerateArray())
                            {
                                fileList.Add(new ManifestFile
                                {
                                    name = file.GetProperty("name").GetString() ?? "",
                                    size = file.GetProperty("size").GetInt64(),
                                    sha256 = file.GetProperty("sha256").GetString() ?? ""
                                });
                            }

                            Console.WriteLine($"Found {fileList.Count} files to download.");
                            bool success = await FileManager.DownloadAllFilesAsync(fileList);
                            _tcs.TrySetResult(success);
                        }
                        else
                        {
                            // Download single file
                            string filename = root.GetProperty("filename").GetString() ?? "";
                            string sha256 = root.GetProperty("sha256").GetString() ?? "";
                            long size = root.GetProperty("size").GetInt64();

                            Console.WriteLine($"Downloading: {filename} ({FormatBytes(size)})");
                            bool success = await FileManager.DownloadFileAsync(filename);
                            _tcs.TrySetResult(success);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error handling download response: {ex.Message}");
                    _tcs.TrySetResult(false);
                }
            };
        }

        public async Task<bool> ExecuteAsync(string target)
        {
            _isAll = (target.ToLowerInvariant() == "all");

            if (_isAll)
            {
                Console.WriteLine("Fetching file list from server...");
                await _client.SendCommandAsync("get-cloud-down", "all");
            }
            else
            {
                Console.WriteLine($"Requesting file: {target}");
                await _client.SendCommandAsync("get-cloud-down", target);
            }

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
