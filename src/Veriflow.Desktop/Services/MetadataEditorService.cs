using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Veriflow.Desktop.Services
{
    public class MetadataEditorService
    {
        // Assume ffmpeg is in path or in a Tools folder relative to the executable
        private string GetFFmpegPath()
        {
            var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");
            if (File.Exists(localPath)) return localPath;
            return "ffmpeg"; // Fallback to system PATH
        }

        public async Task<bool> UpdateMetadataAsync(string filePath, Dictionary<string, string> metadataTags)
        {
            if (!File.Exists(filePath)) return false;

            string ffmpegPath = GetFFmpegPath();
            string extension = Path.GetExtension(filePath);
            string tempFile = Path.Combine(Path.GetDirectoryName(filePath)!, $"_temp{Guid.NewGuid()}{extension}");

            // Preserve original file info for restoration
            var originalCreationTime = File.GetCreationTime(filePath);
            var originalWriteTime = File.GetLastWriteTime(filePath);

            try
            {
                var args = new List<string>
                {
                    "-y", // Overwrite output (temp)
                    "-i", filePath,
                    "-map", "0",
                    "-c", "copy" // Stream copy is fastest and lossless
                };

                foreach (var tag in metadataTags)
                {
                    // FFmpeg syntax: -metadata key="value"
                    // Handle empty values to potentially remove tags? FFmpeg usually expects non-empty. 
                    if (!string.IsNullOrWhiteSpace(tag.Value))
                    {
                        args.Add("-metadata");
                        args.Add($"{tag.Key}={tag.Value}");
                    }
                }

                args.Add(tempFile);

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                // Use ArgumentList for .NET Core / .NET 5+ to handle escaping securely
                foreach (var arg in args) startInfo.ArgumentList.Add(arg);

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    // Optional: Read stderr for logging errors in a real app
                    // await process.StandardError.ReadToEndAsync(); 
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0 && File.Exists(tempFile))
                    {
                        // Success: Swap files
                        File.Delete(filePath);
                        File.Move(tempFile, filePath);

                        // Restore Timestamps
                        try
                        {
                            File.SetCreationTime(filePath, originalCreationTime);
                            File.SetLastWriteTime(filePath, originalWriteTime);
                        }
                        catch { /* Ignore timestamp restoration errors */ }

                        return true;
                    }
                    else
                    {
                        // Failure
                        if (File.Exists(tempFile)) File.Delete(tempFile);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Metadata Update Error: {ex.Message}");
                if (File.Exists(tempFile)) File.Delete(tempFile);
                return false;
            }
        }

        /// <summary>
        /// Updates UCS (Universal Category System) metadata tags in an audio file
        /// </summary>
        public async Task<bool> UpdateUCSMetadataAsync(string filePath, string category, string subCategory, string catId)
        {
            var tags = new Dictionary<string, string>();
            
            if (!string.IsNullOrWhiteSpace(category))
                tags["CATEGORY"] = category;
            
            if (!string.IsNullOrWhiteSpace(subCategory))
                tags["SUBCATEGORY"] = subCategory;
            
            if (!string.IsNullOrWhiteSpace(catId))
                tags["CATID"] = catId;

            return await UpdateMetadataAsync(filePath, tags);
        }
    }
}
