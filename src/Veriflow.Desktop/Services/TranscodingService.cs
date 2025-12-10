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
    }

    public class TranscodeOptions
    {
        public string Format { get; set; } = "WAV";
        public string SampleRate { get; set; } = "Same as Source";
        public string BitDepth { get; set; } = "Same as Source"; // 16, 24, 32
        public string Bitrate { get; set; } = ""; // e.g. "320k", "192k"
    }

    public class SourceMetadata
    {
        public int SampleRate { get; set; }
        public long TimeReference { get; set; }
    }

    public class TranscodingService : ITranscodingService
    {
        public async Task TranscodeAsync(string sourceFile, string outputFile, TranscodeOptions options, IProgress<double>? progress, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found", sourceFile);

            // Ensure output directory exists
            var outDir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            // 1. PRE-ANALYSIS: Get Source Metadata (SampleRate & TimeReference)
            var sourceMeta = await GetMediaMetadataAsync(sourceFile, cancellationToken);
            
            // 2. CALCULATE TIMECODE (Drift Correction)
            long newTimeRef = sourceMeta.TimeReference;
            int targetSampleRate = sourceMeta.SampleRate; // Default to source

            // Parse Target Sample Rate
            if (int.TryParse(options.SampleRate, out int parsedRate))
            {
                targetSampleRate = parsedRate;
            }

            // If Sample Rate changes, rescale TimeReference
            if (sourceMeta.SampleRate > 0 && targetSampleRate != sourceMeta.SampleRate)
            {
                // Formula: New = Old * (Target / Source)
                // Use double for precision then cast back to long
                double ratio = (double)targetSampleRate / sourceMeta.SampleRate;
                newTimeRef = (long)(sourceMeta.TimeReference * ratio);
            }

            // 3. TRANSCODE & RE-INJECT
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

            // Capture stderr for debug or progress
            process.ErrorDataReceived += (s, e) => 
            {
                if (e.Data != null)
                {
                    // Debug output or progress parsing (e.g. time=...)
                }
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

        private async Task<SourceMetadata> GetMediaMetadataAsync(string sourceFile, CancellationToken token)
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

            return meta;
        }

        private string BuildArguments(string source, string output, TranscodeOptions options, long timeReference)
        {
            // Core Flags:
            // -y: Overwrite output
            // -i source: Input file
            // -map_metadata 0: Copy global metadata from input 0
            // -write_bext 1: Force writing BEXT chunk (Broadcast Wave)
            // -metadata time_reference=...: Inject corrected Timecode
            
            var args = $"-y -i \"{source}\" -map_metadata 0 -write_bext 1 -metadata time_reference={timeReference}";

            // Audio Filters
            // Sample Rate
            if (int.TryParse(options.SampleRate, out int rate))
            {
                args += $" -ar {rate}";
            }

            // Codecs & Format Specifics
            string fmt = options.Format.ToUpper();

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
                // LAME encoder, high quality
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
