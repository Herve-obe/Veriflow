using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace Veriflow.Desktop.Services
{
    /// <summary>
    /// Service for synchronizing audio and video files using waveform correlation.
    /// Extracts audio from video, performs cross-correlation to find offset.
    /// </summary>
    public class WaveformSyncService
    {
        private readonly string _ffmpegPath;
        private readonly string _tempDir;
        // private readonly string _logFile; // Removed per user request

        public WaveformSyncService()
        {
            _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            _tempDir = Path.Combine(Path.GetTempPath(), "Veriflow_WaveformSync");
            Directory.CreateDirectory(_tempDir);
            
            // Log file removed
            
            Log("=== Veriflow Waveform Sync Service Started ===");
            Log($"FFmpeg path: {_ffmpegPath}");
            Log($"FFmpeg exists: {File.Exists(_ffmpegPath)}");
            Log($"Temp dir: {_tempDir}");
        }

        private void Log(string message)
        {
            Debug.WriteLine($"[WaveformSync] {message}");
        }

        /// <summary>
        /// Prepares an audio or video file for analysis by extracting/normalizing it to a temp WAV file.
        /// Public to allow caching by the ViewModel.
        /// </summary>
        public async Task<string> PrepareAudioAsync(string sourcePath, bool isVideo, int maxDuration = 10)
        {
            string outputPath = Path.Combine(_tempDir, $"prep_{Guid.NewGuid()}.wav");
            
            // FFmpeg: Convert to mono 16kHz 16-bit PCM (lower quality = faster)
            // -ac 1 = mono, -ar 16000 = 16kHz
            string inputArg = isVideo ? "-vn" : ""; // If video, disable video stream
            string args = $"-i \"{sourcePath}\" {inputArg} -acodec pcm_s16le -ar 16000 -ac 1 -t {maxDuration} -y \"{outputPath}\"";

            // Log($"Preparing audio: {Path.GetFileName(sourcePath)}");
            
            await RunFFmpegAsync(args);
            return outputPath;
        }

        /// <summary>
        /// Calculates offset between two PREPARED wav files using FFT correlation.
        /// Optimized for parallel execution.
        /// </summary>
        public async Task<double?> CalculateOffsetAsync(string wav1Path, string wav2Path)
        {
             return await Task.Run<double?>(() =>
            {
                try
                {
                    // Read WAV files
                    var samples1 = ReadWavSamples(wav1Path);
                    var samples2 = ReadWavSamples(wav2Path);

                    if (samples1 == null || samples2 == null || samples1.Length == 0 || samples2.Length == 0)
                        return null;

                    // Ensure both signals have the same length (pad with zeros if needed)
                    int maxLength = Math.Max(samples1.Length, samples2.Length);
                    
                    // Use power of 2 for FFT efficiency
                    int fftSize = (int)Math.Pow(2, Math.Ceiling(Math.Log(maxLength * 2) / Math.Log(2)));
                    
                    // Convert to complex arrays and pad
                    var complex1 = new System.Numerics.Complex[fftSize];
                    var complex2 = new System.Numerics.Complex[fftSize];

                    for (int i = 0; i < samples1.Length; i++)
                        complex1[i] = new System.Numerics.Complex(samples1[i], 0);
                    
                    for (int i = 0; i < samples2.Length; i++)
                        complex2[i] = new System.Numerics.Complex(samples2[i], 0);

                    // Perform FFT on both signals
                    Fourier.Forward(complex1, FourierOptions.Matlab);
                    Fourier.Forward(complex2, FourierOptions.Matlab);

                    // Cross-correlation in frequency domain
                    var crossPower = new System.Numerics.Complex[fftSize];
                    for (int i = 0; i < fftSize; i++)
                    {
                        crossPower[i] = complex1[i] * System.Numerics.Complex.Conjugate(complex2[i]);
                    }

                    // Inverse FFT
                    Fourier.Inverse(crossPower, FourierOptions.Matlab);

                    // Find peak
                    double maxCorr = double.MinValue;
                    int maxLag = 0;
                    int searchRange = Math.Min(fftSize / 2, maxLength);
                    
                    for (int i = 0; i < searchRange; i++)
                    {
                        if (crossPower[i].Magnitude > maxCorr)
                        {
                            maxCorr = crossPower[i].Magnitude;
                            maxLag = i;
                        }
                    }

                    for (int i = fftSize - searchRange; i < fftSize; i++)
                    {
                        if (crossPower[i].Magnitude > maxCorr)
                        {
                            maxCorr = crossPower[i].Magnitude;
                            maxLag = i - fftSize; 
                        }
                    }

                    double offsetSeconds = (double)maxLag / 16000.0; // 16kHz
                    
                    // Threshold check (optional, but good for filtering noise)
                    if (maxCorr < 0.1) return null; // Very weak correlation

                    return offsetSeconds;
                }
                catch
                {
                    return null;
                }
            });
        }

        // Kept for backward compatibility if needed, using the new optimized methods
        public async Task<double?> FindOffsetAsync(string videoPath, string audioPath, int maxDurationSeconds = 10, IProgress<double>? progress = null)
        {
            string? videoWav = null;
            string? audioWav = null;
            try
            {
                progress?.Report(0.1);
                videoWav = await PrepareAudioAsync(videoPath, true, maxDurationSeconds);
                progress?.Report(0.4);
                audioWav = await PrepareAudioAsync(audioPath, false, maxDurationSeconds);
                progress?.Report(0.7);
                var result = await CalculateOffsetAsync(videoWav, audioWav);
                progress?.Report(1.0);
                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (videoWav != null) CleanupTempFile(videoWav);
                if (audioWav != null) CleanupTempFile(audioWav);
            }
        }



        /// <summary>
        /// Reads WAV file samples (16-bit PCM) and normalizes to -1.0 to 1.0
        /// </summary>
        private double[]? ReadWavSamples(string wavPath)
        {
            try
            {
                using var fs = new FileStream(wavPath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                // Skip WAV header (44 bytes for standard PCM WAV)
                br.ReadBytes(44);

                // Read samples
                var samples = new List<double>();
                while (fs.Position < fs.Length)
                {
                    short sample = br.ReadInt16(); // 16-bit PCM
                    double normalized = sample / 32768.0; // Normalize to -1.0 to 1.0
                    samples.Add(normalized);
                }

                return samples.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WaveformSync] Error reading WAV: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Runs FFmpeg command asynchronously
        /// </summary>
        private async Task RunFFmpegAsync(string arguments)
        {
            Log($"Running FFmpeg...");
            Log($"FFmpeg path: {_ffmpegPath}");
            Log($"FFmpeg exists: {File.Exists(_ffmpegPath)}");
            
            if (!File.Exists(_ffmpegPath))
            {
                throw new FileNotFoundException($"FFmpeg not found at: {_ffmpegPath}");
            }
            
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = new Process { StartInfo = startInfo };
            
            Log($"Starting FFmpeg process...");
            process.Start();
            
            // CRITICAL FIX: Read streams asynchronously DURING execution to prevent deadlock
            // If we wait for exit before reading, FFmpeg can hang if buffers fill up
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            Log($"Waiting for FFmpeg to complete...");
            await process.WaitForExitAsync();
            
            string output = await outputTask;
            string error = await errorTask;
            
            Log($"FFmpeg exited with code: {process.ExitCode}");

            if (process.ExitCode != 0)
            {
                Log($"✗ FFmpeg failed with exit code: {process.ExitCode}");
                Log($"Error output: {error}");
                throw new Exception($"FFmpeg error (exit code {process.ExitCode}): {error}");
            }
            
            Log($"✓ FFmpeg completed successfully");
        }

        /// <summary>
        /// Cleans up temporary file
        /// </summary>
        private void CleanupTempFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        /// <summary>
        /// Cleans up all temporary files in the temp directory
        /// </summary>
        public void CleanupAll()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }
    }
}
