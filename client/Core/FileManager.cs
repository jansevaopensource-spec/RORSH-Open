// RORSH-Gate File Manager
// Handles local file operations, downloads, SHA-256 verification

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace RorshGate.Core
{
    public static class FileManager
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        static FileManager()
        {
            HttpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        // List local downloaded files
        public static List<string> ListLocalFiles()
        {
            if (!Directory.Exists(Config.DownloadsDirectory))
            {
                return new List<string>();
            }

            return Directory.GetFiles(Config.DownloadsDirectory)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .ToList();
        }

        // Check if file exists locally
        public static bool FileExistsLocally(string filename)
        {
            string path = Path.Combine(Config.DownloadsDirectory, filename);
            return File.Exists(path);
        }

        // Get local file path
        public static string GetLocalFilePath(string filename)
        {
            return Path.Combine(Config.DownloadsDirectory, filename);
        }

        // Compute SHA-256 of a local file
        public static async Task<string> ComputeSha256Async(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // Download file with SHA-256 verification
        public static async Task<bool> DownloadFileAsync(string filename)
        {
            try
            {
                string sha256Url = $"{Config.SERVER_HTTP_URL}/sha256/{Uri.EscapeDataString(filename)}";
                string downloadUrl = $"{Config.SERVER_HTTP_URL}/download/{Uri.EscapeDataString(filename)}";

                // Step 1: Get SHA-256 from server
                Logger.Info($"Fetching SHA-256 for: {filename}");
                string sha256Response = await HttpClient.GetStringAsync(sha256Url);
                var shaData = JsonSerializer.Deserialize<Sha256Response>(sha256Response);

                if (shaData == null || string.IsNullOrEmpty(shaData.sha256))
                {
                    Logger.Error("Failed to get SHA-256 from server");
                    return false;
                }

                string expectedSha256 = shaData.sha256;
                Logger.Info($"Expected SHA-256: {expectedSha256}");

                // Step 2: Download the file
                Logger.Info($"Downloading: {filename}");
                string tempPath = Path.Combine(Config.DownloadsDirectory, $"{filename}.tmp");

                using (var response = await HttpClient.GetAsync(downloadUrl))
                {
                    response.EnsureSuccessStatusCode();
                    await using var fs = new FileStream(tempPath, FileMode.Create);
                    await response.Content.CopyToAsync(fs);
                }

                // Step 3: Verify SHA-256
                Logger.Info("Verifying SHA-256...");
                string actualSha256 = await ComputeSha256Async(tempPath);

                if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Error($"SHA-256 mismatch! Expected: {expectedSha256}, Got: {actualSha256}");
                    File.Delete(tempPath);
                    return false;
                }

                Logger.Info("SHA-256 verified successfully");

                // Step 4: Move to final location
                string finalPath = Path.Combine(Config.DownloadsDirectory, filename);
                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }
                File.Move(tempPath, finalPath);

                Logger.Info($"Download complete: {filename}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Download failed: {ex.Message}");
                return false;
            }
        }

        // Download all files from manifest
        public static async Task<bool> DownloadAllFilesAsync(List<ManifestFile> files)
        {
            int successCount = 0;
            int failCount = 0;

            foreach (var file in files)
            {
                Logger.Info($"Downloading ({successCount + failCount + 1}/{files.Count}): {file.name}");
                bool success = await DownloadFileAsync(file.name);
                if (success)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }

            Logger.Info($"Download complete: {successCount} succeeded, {failCount} failed");
            return failCount == 0;
        }

        private class Sha256Response
        {
            public string filename { get; set; } = string.Empty;
            public string sha256 { get; set; } = string.Empty;
        }
    }

    public class ManifestFile
    {
        public string name { get; set; } = string.Empty;
        public long size { get; set; }
        public string sha256 { get; set; } = string.Empty;
    }
}
