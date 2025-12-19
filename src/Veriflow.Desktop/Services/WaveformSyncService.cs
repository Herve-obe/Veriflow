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

        public WaveformSyncService()
        {
            _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            _tempDir = Path.Combine(Path.GetTempPath(), "Veriflow_WaveformSync");
            Directory.CreateDirectory(_tempDir);
        }

        /// <summary>
        /// Finds the time offset between video and audio files using waveform correlation.
        /// Optimized for speed like DaVinci Resolve.
        /// </summary>
        /// <param name="videoPath">Path to video file</param>
        /// <param name="audioPath">Path to audio file</param>
        /// <param name="maxDurationSeconds">Maximum duration to analyze (default: 10s for speed)</param>
        /// <param name="progress">Optional progress callback (0.0 to 1.0)</param>
        /// <returns>Offset in seconds (positive = audio ahead, negative = audio behind)</returns>
        public async Task<double?> FindOffsetAsync(string videoPath, string audioPath, int maxDurationSeconds = 10, IProgress<double>? progress = null)
        {
            try
            {
                progress?.Report(0.1);
                Debug.WriteLine($"[WaveformSync] Starting analysis: {Path.GetFileName(videoPath)} + {Path.GetFileName(audioPath)}");

                // Extract audio from video (optimized - low quality for speed)
                string videoAudioPath = await ExtractAudioFromVideoAsync(videoPath, maxDurationSeconds);
                progress?.Report(0.3);

                // Extract audio (normalize to same format)
                string normalizedAudioPath = await NormalizeAudioAsync(audioPath, maxDurationSeconds);
                progress?.Report(0.5);

                // Perform cross-correlation (optimized)
                double? offset = await PerformCrossCorrelationAsync(videoAudioPath, normalizedAudioPath);
                progress?.Report(0.9);

                // Cleanup temp files
                CleanupTempFile(videoAudioPath);
                CleanupTempFile(normalizedAudioPath);

                progress?.Report(1.0);
                
                if (offset.HasValue)
                {
                    Debug.WriteLine($"[WaveformSync] SUCCESS: Offset = {offset.Value:F3}s");
                }
                else
                {
                    Debug.WriteLine($"[WaveformSync] FAILED: No correlation found");
                }
                
                return offset;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WaveformSync] ERROR: {ex.Message}");
                Debug.WriteLine($"[WaveformSync] Stack: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Extracts audio from video file to WAV format (mono, 16kHz, 16-bit for speed)
        /// </summary>
        private async Task<string> ExtractAudioFromVideoAsync(string videoPath, int maxDuration)
        {
            string outputPath = Path.Combine(_tempDir, $"video_audio_{Guid.NewGuid()}.wav");
            
            // FFmpeg: Extract audio, convert to mono 16kHz 16-bit PCM (lower quality = faster)
            // -ac 1 = mono, -ar 16000 = 16kHz (vs 48kHz), -t = duration limit
            string args = $"-i \"{videoPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 -t {maxDuration} -y \"{outputPath}\"";

            Debug.WriteLine($"[WaveformSync] Extracting video audio: {Path.GetFileName(videoPath)}");
            await RunFFmpegAsync(args);
            Debug.WriteLine($"[WaveformSync] Video audio extracted: {outputPath}");
            
            return outputPath;
        }

        /// <summary>
        /// Normalizes audio file to WAV format (mono, 16kHz, 16-bit for speed)
        /// </summary>
        private async Task<string> NormalizeAudioAsync(string audioPath, int maxDuration)
        {
            string outputPath = Path.Combine(_tempDir, $"normalized_audio_{Guid.NewGuid()}.wav");
            
            // FFmpeg: Convert to mono 16kHz 16-bit PCM (lower quality = faster)
            string args = $"-i \"{audioPath}\" -acodec pcm_s16le -ar 16000 -ac 1 -t {maxDuration} -y \"{outputPath}\"";

            Debug.WriteLine($"[WaveformSync] Normalizing audio: {Path.GetFileName(audioPath)}");
            await RunFFmpegAsync(args);
            Debug.WriteLine($"[WaveformSync] Audio normalized: {outputPath}");
            
            return outputPath;
        }

        /// <summary>
        /// Performs FFT-based cross-correlation between two WAV files to find offset.
        /// This is the proper way - much faster than naive sliding window.
        /// </summary>
        private Task<double?> PerformCrossCorrelationAsync(string wavFile1, string wavFile2)
        {
            return Task.Run<double?>(() =>
            {
                try
                {
                    Debug.WriteLine("[WaveformSync] Reading WAV samples...");
                    
                    // Read WAV files
                    var samples1 = ReadWavSamples(wavFile1);
                    var samples2 = ReadWavSamples(wavFile2);

                    if (samples1 == null || samples2 == null || samples1.Length == 0 || samples2.Length == 0)
                    {
                        Debug.WriteLine("[WaveformSync] Failed to read samples");
                        return null;
                    }

                    Debug.WriteLine($"[WaveformSync] Samples read: {samples1.Length} vs {samples2.Length}");

                    // Ensure both signals have the same length (pad with zeros if needed)
                    int maxLength = Math.Max(samples1.Length, samples2.Length);
                    
                    // Use power of 2 for FFT efficiency
                    int fftSize = (int)Math.Pow(2, Math.Ceiling(Math.Log(maxLength * 2) / Math.Log(2)));
                    
                    Debug.WriteLine($"[WaveformSync] FFT size: {fftSize}");

                    // Convert to complex arrays and pad
                    var complex1 = new System.Numerics.Complex[fftSize];
                    var complex2 = new System.Numerics.Complex[fftSize];

                    for (int i = 0; i < samples1.Length; i++)
                        complex1[i] = new System.Numerics.Complex(samples1[i], 0);
                    
                    for (int i = 0; i < samples2.Length; i++)
                        complex2[i] = new System.Numerics.Complex(samples2[i], 0);

                    Debug.WriteLine("[WaveformSync] Performing FFT...");

                    // Perform FFT on both signals
                    Fourier.Forward(complex1, FourierOptions.Matlab);
                    Fourier.Forward(complex2, FourierOptions.Matlab);

                    // Cross-correlation in frequency domain: 
                    // corr = IFFT(FFT(signal1) * conj(FFT(signal2)))
                    var crossPower = new System.Numerics.Complex[fftSize];
                    for (int i = 0; i < fftSize; i++)
                    {
                        crossPower[i] = complex1[i] * System.Numerics.Complex.Conjugate(complex2[i]);
                    }

                    Debug.WriteLine("[WaveformSync] Performing inverse FFT...");

                    // Inverse FFT to get cross-correlation
                    Fourier.Inverse(crossPower, FourierOptions.Matlab);

                    Debug.WriteLine("[WaveformSync] Finding peak...");

                    // Find the peak in the cross-correlation
                    double maxCorr = double.MinValue;
                    int maxLag = 0;

                    // Only search reasonable range (not the entire FFT size)
                    int searchRange = Math.Min(fftSize / 2, maxLength);
                    
                    for (int i = 0; i < searchRange; i++)
                    {
                        double magnitude = crossPower[i].Magnitude;
                        if (magnitude > maxCorr)
                        {
                            maxCorr = magnitude;
                            maxLag = i;
                        }
                    }

                    // Also check negative lags (second half of FFT)
                    for (int i = fftSize - searchRange; i < fftSize; i++)
                    {
                        double magnitude = crossPower[i].Magnitude;
                        if (magnitude > maxCorr)
                        {
                            maxCorr = magnitude;
                            maxLag = i - fftSize; // Negative lag
                        }
                    }

                    // Convert lag to seconds
                    double sampleRate = 16000; // 16kHz
                    double offsetSeconds = (double)maxLag / sampleRate;

                    Debug.WriteLine($"[WaveformSync] Correlation complete: maxCorr={maxCorr:F2}, maxLag={maxLag}, offset={offsetSeconds:F3}s");
                    
                    return (double?)offsetSeconds;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WaveformSync] Correlation error: {ex.Message}");
                    Debug.WriteLine($"[WaveformSync] Stack: {ex.StackTrace}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Calculates correlation between two signals at a given lag (DEPRECATED - using FFT now)
        /// </summary>
        private double CalculateCorrelation(double[] reference, double[] signal, int lag)
        {
            double sum = 0;
            int count = 0;

            for (int i = 0; i < signal.Length; i++)
            {
                if (lag + i < reference.Length)
                {
                    sum += reference[lag + i] * signal[i];
                    count++;
                }
            }

            return count > 0 ? sum / count : 0;
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
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"FFmpeg error: {error}");
            }
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
