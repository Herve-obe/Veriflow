using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Veriflow.Desktop.Services
{
    public interface ITranscodingService
    {
        Task TranscodeAsync(string sourceFile, string outputFile, TranscodeOptions options, IProgress<double>? progress, CancellationToken cancellationToken = default);
        Task<SourceMetadata> GetMediaMetadataAsync(string sourceFile, CancellationToken cancellationToken = default);
    }

    public class TranscodeOptions
    {
        public string Format { get; set; } = "WAV";
        public string SampleRate { get; set; } = "Same as Source";
        public string BitDepth { get; set; } = "Same as Source"; // Audio: 16, 24, 32
        public string Bitrate { get; set; } = ""; // e.g. "320k", "192k"
        
        // Video Specific
        public string VideoBitDepth { get; set; } = "Same as Source"; // "8-bit", "10-bit"
    }

    public class SourceMetadata
    {
        public int SampleRate { get; set; }
        public long TimeReference { get; set; } // Samples
        public TimeSpan Duration { get; set; }
        public string CodecInfo { get; set; } = "Unknown";
    }



    public class TranscodingService : ITranscodingService
    {
        // ... (TranscodeAsync and GetMediaMetadataAsync remain unchanged) ...

        public async Task TranscodeAsync(string sourceFile, string outputFile, TranscodeOptions options, IProgress<double>? progress, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found", sourceFile);

            var outDir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

            // 1. ANALYSIS (Keep Audio Logic)
            var sourceMeta = await GetMediaMetadataAsync(sourceFile, cancellationToken);
            long newTimeRef = sourceMeta.TimeReference;
            int targetSampleRate = sourceMeta.SampleRate > 0 ? sourceMeta.SampleRate : 48000;

            if (int.TryParse(options.SampleRate, out int parsedRate)) targetSampleRate = parsedRate;

            if (sourceMeta.SampleRate > 0 && targetSampleRate != sourceMeta.SampleRate)
            {
                double ratio = (double)targetSampleRate / sourceMeta.SampleRate;
                newTimeRef = (long)(sourceMeta.TimeReference * ratio);
            }

            // 2. BUILD ARGS
            string ffmpegArgs = BuildArguments(sourceFile, outputFile, options, newTimeRef);
            string ffmpegPath = GetToolPath("ffmpeg");

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                WorkingDirectory = Path.GetDirectoryName(ffmpegPath),
                Arguments = ffmpegArgs,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            var tcs = new TaskCompletionSource<bool>();
            process.EnableRaisingEvents = true;
            
            process.Exited += (s, e) =>
            {
                if (process.ExitCode == 0) tcs.TrySetResult(true);
                else tcs.TrySetException(new Exception($"FFmpeg exited with code {process.ExitCode}"));
            };

            process.Start();
            process.BeginErrorReadLine();
            
            try 
            {
                await tcs.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); } catch { } 
                throw;
            }
        }

        public async Task<SourceMetadata> GetMediaMetadataAsync(string sourceFile, CancellationToken token = default)
        {
            var meta = new SourceMetadata();
            string ffprobePath = GetToolPath("ffprobe");

            // ffprobe args: json format, show format and streams to catch metadata everywhere
            string args = $"-v quiet -print_format json -show_format -show_streams -i \"{sourceFile}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string jsonOutput = await process.StandardOutput.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);

            // 1. Sample Rate (Stream level preferred)
            var srMatch = System.Text.RegularExpressions.Regex.Match(jsonOutput, "\"sample_rate\":\\s*\"(\\d+)\"");
            if (srMatch.Success && int.TryParse(srMatch.Groups[1].Value, out int sr))
            {
                meta.SampleRate = sr;
            }

            // 2. Time Reference (BEXT / encoded_by / time_reference tag)
            var trMatch = System.Text.RegularExpressions.Regex.Match(jsonOutput, "\"time_reference\":\\s*\"(\\d+)\"");
            if (trMatch.Success && long.TryParse(trMatch.Groups[1].Value, out long tr))
            {
                meta.TimeReference = tr;
            }

            // 3. Duration
            var durMatch = System.Text.RegularExpressions.Regex.Match(jsonOutput, "\"duration\":\\s*\"([\\d\\.]+)\"");
            if (durMatch.Success && double.TryParse(durMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double durSec))
            {
                meta.Duration = TimeSpan.FromSeconds(durSec);
            }

            // 4. Codec / Format (Find first codec_name)
            var codecMatch = System.Text.RegularExpressions.Regex.Match(jsonOutput, "\"codec_name\":\\s*\"([^\"]+)\"");
            if (codecMatch.Success)
            {
                string codec = codecMatch.Groups[1].Value;
                // Try to find video codec if multiple streams exist
                // Simple hack: if codec is 'audio', look for next? No, just take what we find or iterate logic.
                // For this, taking the first valid codec name is 'good enough' for general ID.
                meta.CodecInfo = codec.ToUpper(); 
                
                // Refinment: Check if video stream exists
                if (jsonOutput.Contains("\"codec_type\": \"video\""))
                {
                    // Find video codec specifically?
                    // Regex for video codec is complex without full JSON parsing. 
                    // Let's stick to simple "Format Name" from container if possible
                    var fmtMatch = System.Text.RegularExpressions.Regex.Match(jsonOutput, "\"format_name\":\\s*\"([^\"]+)\"");
                    if (fmtMatch.Success) meta.CodecInfo += $" ({fmtMatch.Groups[1].Value})";
                }
            }
            
            // Bit Depth (sample_fmt or bits_per_raw_sample)
            // ...

            return meta;
        }

        private string BuildArguments(string source, string output, TranscodeOptions options, long timeReference)
        {
            // Base Args: Input, Map Metadata
            var args = $"-y -i \"{source}\" -map_metadata 0";
            
            bool isVideo = options.Format.Contains("H.264") || options.Format.Contains("H.265") || options.Format.Contains("ProRes") || options.Format.Contains("DNxHD");
            
            // Timecode injection (Video & Audio mostly same flag, but Video uses -timecode usually for MOV/MXF)
            // Audio uses -write_bext 1 -metadata time_reference
            // Let's try to apply both or specific based on container.
            
            if (!isVideo)
            {
                args += $" -write_bext 1 -metadata time_reference={timeReference}";
            }
            // For Video, we might need "-timecode HH:MM:SS:FF" but that requires calculation.
            // For now, let's assume map_metadata carries it over for video containers.

            string fmt = options.Format.ToUpper();

            // VIDEO LOGIC
            if (isVideo)
            {
                // Video Codec & Profile
                if (fmt.Contains("H.264"))
                {
                    args += " -c:v libx264 -preset slow -crf 18";
                    
                    // Bit Depth / Pixel Format
                    if (options.VideoBitDepth == "10-bit") args += " -pix_fmt yuv420p10le";
                    else args += " -pix_fmt yuv420p"; // Default 8-bit
                }
                else if (fmt.Contains("H.265"))
                {
                    args += " -c:v libx265 -preset slow -crf 20"; // x265 is more efficient
                    
                    // Bit Depth / Pixel Format
                    if (options.VideoBitDepth == "10-bit") args += " -pix_fmt yuv420p10le";
                    else args += " -pix_fmt yuv420p"; 
                }
                else if (fmt.Contains("PRORES"))
                {
                    args += " -c:v prores_ks"; // prores_ks is the high quality implementation
                    
                    // Profile Mapping
                    // proxy=0, lt=1, standard=2, hq=3, 4444=4, 4444xq=5
                    if (fmt.Contains("PROXY")) args += " -profile:v 0";
                    else if (fmt.Contains("LT")) args += " -profile:v 1";
                    else if (fmt.Contains("HQ")) args += " -profile:v 3";
                    else if (fmt.Contains("4444")) args += " -profile:v 4 -pix_fmt yuv444p10le"; 
                    else args += " -profile:v 2"; // Standard

                    // ProRes is usually 10-bit by definition (except 4444 which is 10/12). 
                    // If user forces 8-bit? ProRes doesn't really do 8-bit. Ignore option or force 10-bit.
                    if (!fmt.Contains("4444")) args += " -pix_fmt yuv422p10le";
                    
                    args += " -vendor apl0"; // Compatibility
                }
                else if (fmt.Contains("DNXHD"))
                {
                    args += " -c:v dnxhd";
                    
                    // DNxHD requires specific bitrate/resolution combos. 
                    // This is complex. For now, let's use -profile:v if available or generic params.
                    // Actually, simpler to map to specific bitrates or use dnxhr for resolution independence.
                    // Let's assume DNxHR for flexibility if possible, or sticking to strict HD logic?
                    // "DNxHD LB" Usually implies DNxHR LB for modern workflows or specific bitrate.
                    // Let's use DNxHR profiles which are resolution independent.
                    
                    if (fmt.Contains("LB")) args += " -profile:v dnxhr_lb -pix_fmt yuv422p";
                    else if (fmt.Contains("SQ")) args += " -profile:v dnxhr_sq -pix_fmt yuv422p";
                    else if (fmt.Contains("HQ")) args += " -profile:v dnxhr_hq -pix_fmt yuv422p";
                    else if (fmt.Contains("444")) args += " -profile:v dnxhr_444 -pix_fmt yuv444p10le";
                }

                // Audio for Video
                // If MP4 container -> AAC
                if (fmt.Contains("MP4") || fmt.Contains("H.264") || fmt.Contains("H.265"))
                {
                    args += " -c:a aac -b:a 320k";
                }
                else // MOV / MXF (ProRes, DNxHD) -> PCM
                {
                    if (options.BitDepth == "24-bit") args += " -c:a pcm_s24le";
                    else if (options.BitDepth == "32-bit") args += " -c:a pcm_s32le";
                    else args += " -c:a pcm_s16le";
                }
            }
            // AUDIO LOGIC
            else
            {
                 // Audio Filters
                if (int.TryParse(options.SampleRate, out int rate))
                {
                    args += $" -ar {rate}";
                }
                
                if (fmt == "WAV")
                {
                    // FORCE RIFF HEADER (Disable RF64) for compatibility with older tools/NAudio default
                    args += " -f wav -rf64 never"; 

                    // PCM Map
                    if (options.BitDepth == "16-bit") args += " -c:a pcm_s16le";
                    else if (options.BitDepth == "24-bit") args += " -c:a pcm_s24le";
                    else if (options.BitDepth == "32-bit Float") args += " -c:a pcm_f32le";
                    else if (options.BitDepth == "32-bit") args += " -c:a pcm_s32le"; // Integer 32-bit
                }
                else if (fmt == "MP3")
                {
                    string br = !string.IsNullOrEmpty(options.Bitrate) ? options.Bitrate : "320k";
                    if (int.TryParse(br, out _)) br += "k";
                    args += $" -c:a libmp3lame -b:a {br} -map_metadata 0 -id3v2_version 3 -write_id3v1 1";
                }
                else if (fmt == "FLAC")
                {
                    args += " -c:a flac";
                }
                else if (fmt == "AAC")
                {
                    string br = !string.IsNullOrEmpty(options.Bitrate) ? options.Bitrate : "256k";
                    if (int.TryParse(br, out _)) br += "k";
                    args += $" -c:a aac -b:a {br}";
                }
                else if (fmt == "OGG")
                {
                    string br = !string.IsNullOrEmpty(options.Bitrate) ? options.Bitrate : "192k";
                    if (int.TryParse(br, out _)) br += "k";
                    args += $" -c:a libvorbis -b:a {br}";
                }
            }

            args += $" \"{output}\"";
            return args;
        }

        private string GetToolPath(string toolName)
        {
            if (!toolName.EndsWith(".exe")) toolName += ".exe";

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            var path = Path.Combine(baseDir, toolName);
            if (File.Exists(path)) return path;

            path = Path.Combine(baseDir, "ExternalTools", toolName);
            if (File.Exists(path)) return path;

            return toolName; // PATH fallback
        }
    }
}
