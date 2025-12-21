using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Veriflow.Desktop.Services
{
    /// <summary>
    /// Service to detect available FFmpeg hardware encoders.
    /// Caches results to avoid repeated process spawning.
    /// </summary>
    public class EncoderDetectionService
    {
        private static EncoderDetectionService? _instance;
        private static readonly object _lock = new object();
        
        private List<string>? _cachedEncoders;
        private bool _detectionComplete = false;

        public static EncoderDetectionService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new EncoderDetectionService();
                        }
                    }
                }
                return _instance;
            }
        }

        private EncoderDetectionService() { }

        /// <summary>
        /// Detects available H.264 encoders by executing 'ffmpeg -encoders'.
        /// Returns a list of encoder names (e.g., "libx264", "h264_nvenc", "h264_qsv").
        /// </summary>
        public async Task<List<string>> GetAvailableEncodersAsync()
        {
            if (_detectionComplete && _cachedEncoders != null)
            {
                return _cachedEncoders;
            }

            var encoders = new List<string>();

            try
            {
                string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                
                if (!File.Exists(ffmpegPath))
                {
                    Debug.WriteLine("[EncoderDetectionService] ffmpeg.exe not found. Defaulting to CPU only.");
                    encoders.Add("libx264"); // CPU fallback
                    _cachedEncoders = encoders;
                    _detectionComplete = true;
                    return encoders;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-encoders",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    encoders = ParseEncoders(output);
                }
                else
                {
                    Debug.WriteLine($"[EncoderDetectionService] ffmpeg -encoders failed with exit code {process.ExitCode}");
                    encoders.Add("libx264"); // Fallback
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EncoderDetectionService] Error detecting encoders: {ex.Message}");
                encoders.Add("libx264"); // Safe fallback
            }

            _cachedEncoders = encoders;
            _detectionComplete = true;
            return encoders;
        }

        /// <summary>
        /// Parses the output of 'ffmpeg -encoders' to extract H.264 encoder names.
        /// </summary>
        private List<string> ParseEncoders(string output)
        {
            var encoders = new List<string>();
            
            // Always include CPU encoder
            encoders.Add("libx264");

            // Regex to match encoder lines: " V..... h264_nvenc           NVIDIA NVENC H.264 encoder"
            // Format: " V..... <encoder_name>       <description>"
            var regex = new Regex(@"^\s*V[\.\w]{5}\s+(h264_\w+|libx264)\s+", RegexOptions.Multiline);
            var matches = regex.Matches(output);

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    string encoderName = match.Groups[1].Value.Trim();
                    
                    // Filter for H.264 hardware encoders
                    if ((encoderName.StartsWith("h264_") || encoderName == "libx264") && !encoders.Contains(encoderName))
                    {
                        encoders.Add(encoderName);
                    }
                }
            }

            Debug.WriteLine($"[EncoderDetectionService] Detected encoders: {string.Join(", ", encoders)}");
            return encoders.Distinct().ToList();
        }

        /// <summary>
        /// Checks if a specific encoder is available.
        /// </summary>
        public async Task<bool> IsEncoderAvailableAsync(string encoderName)
        {
            var available = await GetAvailableEncodersAsync();
            return available.Contains(encoderName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a user-friendly display name for an encoder.
        /// </summary>
        public string GetEncoderDisplayName(string encoderName)
        {
            return encoderName switch
            {
                "libx264" => "CPU (libx264)",
                "h264_nvenc" => "NVIDIA NVENC",
                "h264_qsv" => "Intel Quick Sync",
                "h264_amf" => "AMD AMF",
                "h264_videotoolbox" => "Apple VideoToolbox",
                _ => encoderName
            };
        }
    }
}
