using System;
using System.IO;
using System.IO.Hashing;
using System.Threading;
using System.Threading.Tasks;

namespace Veriflow.Desktop.Services
{
    public class SecureCopyService
    {
        private const int BufferSize = 1024 * 1024 * 4; // 4MB Buffer

        /// <summary>
        /// Copies a file with strict Read-Back Verification (Silverstack Level).
        /// 1. Reads Source -> Calculates Hash A -> Writes to Dest.
        /// 2. Reads Dest -> Calculates Hash B.
        /// 3. Verify A == B.
        /// </summary>
        /// <summary>
        /// Optimized Single-Read / Multi-Write Secure Offload.
        /// - Reads Source ONCE.
        /// - Writes to N destinations simultaneously.
        /// - Verifies N destinations (Read-Back) independently.
        /// </summary>
        public async Task<List<CopyResult>> CopyFileMultiDestSecureAsync(string sourcePath, List<string> destPaths, IProgress<CopyProgress> progress, CancellationToken ct)
        {
            var results = destPaths.Select(d => new CopyResult 
            { 
                Success = false, 
                Filename = Path.GetFileName(sourcePath), 
                SourcePath = sourcePath, 
                DestPath = d,
                Timestamp = DateTime.Now 
            }).ToList();

            var fileLength = new FileInfo(sourcePath).Length;
            
            // Validate Dests & Create Dirs
            var activeDestinations = new List<int>(); // Indexes of valid destinations
            for (int i = 0; i < destPaths.Count; i++)
            {
                try 
                {
                    var destDir = Path.GetDirectoryName(destPaths[i]);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                    
                    // Pre-flight Write Check
                    VerifyWritePermission(destDir ?? "");

                    activeDestinations.Add(i);
                }
                catch (Exception ex)
                {
                    results[i].Note = $"Init Failed (Write Check): {ex.Message}";
                }
            }

            if (activeDestinations.Count == 0) return results; // Fail all

            // --- PHASE 1: COPY (Single Read, Multi Write) ---
            var xxHashSource = new XxHash64();
            var buffer = new byte[BufferSize];
            long totalRead = 0;
            var startTime = DateTime.UtcNow;

            // Open Source
            FileStream? sourceStream = null;
            var destStreams = new Dictionary<int, FileStream>();

            try
            {
                try
                {
                    sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true); // Sequential scan hint?
                    
                    // Open Dest Streams
                    foreach (var idx in activeDestinations)
                    {
                        try
                        {
                            destStreams[idx] = new FileStream(destPaths[idx], FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);
                        }
                        catch (Exception ex)
                        {
                            results[idx].Note = $"Write Open Failed: {ex.Message}";
                            results[idx].Success = false;
                            destStreams.Remove(idx); // Key check handled by iteration logic
                        }
                    }

                    // If no dests managed to open, abort
                    if (destStreams.Count == 0) throw new Exception("No destination streams could be opened.");

                    int read;
                    while ((read = await sourceStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();

                        // 1. Hash Source
                        xxHashSource.Append(buffer.AsSpan(0, read));

                        // 2. Write to ALL active streams parallel-ish
                        var writeTasks = destStreams.Select(async kvp => 
                        {
                            try
                            {
                                await kvp.Value.WriteAsync(buffer, 0, read, ct);
                            }
                            catch (Exception ex)
                            {
                                // If write fails, mark invalid and track to remove later? 
                                // Cannot modify dictionary while iterating.
                                // We catch here, but we need to mark this stream as dead.
                                 return (kvp.Key, ex);
                            }
                            return (kvp.Key, (Exception?)null); // Success
                        });

                        var outcomes = await Task.WhenAll(writeTasks);

                        // Handle Write Failures mid-stream
                        foreach (var (key, error) in outcomes)
                        {
                            if (error != null)
                            {
                                // Cleanup failed stream
                                 if (destStreams.ContainsKey(key))
                                 {
                                     try { destStreams[key].Dispose(); } catch { }
                                     destStreams.Remove(key);
                                     results[key].Note = $"Write Failed: {error.Message}";
                                     results[key].Success = false;
                                 }
                            }
                        }

                        if (destStreams.Count == 0) break; // All died

                        totalRead += read;
                        ReportProgress(progress, totalRead, fileLength, startTime, "Copying");
                    }
                    
                    var finalSourceHash = BitConverter.ToString(xxHashSource.GetCurrentHash()).Replace("-", "").ToLowerInvariant();
                    foreach(var r in results) r.SourceHash = finalSourceHash;

                }
                finally
                {
                    sourceStream?.Dispose();
                    foreach (var s in destStreams.Values) try { s.Dispose(); } catch { }
                }

                // --- PHASE 2: VERIFY (Parallel Read-Back) ---
                // Only verify successfully written dests
                var verifyTasks = activeDestinations
                    .Where(i => destStreams.ContainsKey(i)) // Only those that survived the write loop
                    .Select(async i => 
                    {
                        try
                        {
                            var hash = await Task.Run(async () => await CalculateFileHashAsync(destPaths[i], ct));
                            results[i].DestHash = hash;
                            
                            if (results[i].SourceHash == hash)
                            {
                                results[i].Success = true;
                                results[i].Note = "Verified (xxHash64)";
                            }
                            else
                            {
                                results[i].Success = false;
                                results[i].Note = "CHECKSUM MISMATCH";
                            }
                        }
                        catch (Exception ex)
                        {
                            results[i].Success = false;
                            results[i].Note = $"Verify Failed: {ex.Message}";
                            if (ex is OperationCanceledException) throw; // Rethrow to trigger outer cleanup
                        }
                        // Calc speed based on total time (Copy + Verify) or just Copy? Use Copy time usually.
                        results[i].AverageSpeed = (fileLength / 1024.0 / 1024.0) / (DateTime.UtcNow - startTime).TotalSeconds;
                    });

                await Task.WhenAll(verifyTasks);
                return results;

            }
            catch (OperationCanceledException)
            {
                // CRITICAL: Cleanup files created in this session if execution is cancelled.
                foreach (var idx in activeDestinations)
                {
                    try 
                    { 
                        if (File.Exists(destPaths[idx])) File.Delete(destPaths[idx]); 
                    } 
                    catch { }
                }
                throw;
            }
            catch (Exception ex)
            {
                 foreach(var idx in activeDestinations) 
                    if (results[idx].Note == "") results[idx].Note = $"Copy Error: {ex.Message}";
                 return results; // Return failures
            }
        }

        private async Task<string> CalculateFileHashAsync(string path, CancellationToken ct)
        {
             var xxHash = new XxHash64();
             var buffer = new byte[BufferSize];
             using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
             int read;
             while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
             {
                 xxHash.Append(buffer.AsSpan(0, read));
             }
             return BitConverter.ToString(xxHash.GetCurrentHash()).Replace("-", "").ToLowerInvariant();
        }

        private void ReportProgress(IProgress<CopyProgress> progress, long currentBytes, long totalBytes, DateTime startTime, string status)
        {
            if (progress == null) return;

            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            var speed = elapsed > 0 ? (currentBytes / 1024.0 / 1024.0) / elapsed : 0;

            progress.Report(new CopyProgress
            {
                Percentage = (double)currentBytes / totalBytes * 100,
                TransferSpeedMbPerSec = speed,
                BytesTransferred = currentBytes,
                TotalBytes = totalBytes,
                Status = status
            });
        }

        public async Task GenerateOffloadReportAsync(string destinationRoot, System.Collections.Generic.List<CopyResult> results)
        {
            await Task.Run(() =>
            {
                try
                {
                    string reportPath = Path.Combine(destinationRoot, $"OFFLOAD_REPORT_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    
                    using (var writer = new StreamWriter(reportPath))
                    {
                        writer.WriteLine("==========================================================================================");
                        writer.WriteLine($" VERIFLOW OFFLOAD REPORT");
                        writer.WriteLine("==========================================================================================");
                        writer.WriteLine($" Date       : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($" User       : {Environment.UserName}");
                        writer.WriteLine($" Computer   : {Environment.MachineName}");
                        writer.WriteLine($" Destination: {destinationRoot}");
                        writer.WriteLine("==========================================================================================");
                        writer.WriteLine("");
                        writer.WriteLine(" SUMMARY");
                        writer.WriteLine(" -------");
                        writer.WriteLine($" Total Files   : {results.Count}");
                        writer.WriteLine($" Success       : {results.Count(r => r.Success)}");
                        writer.WriteLine($" Failed        : {results.Count(r => !r.Success)}");
                        writer.WriteLine($" Total Size    : {results.Sum(r => new FileInfo(r.SourcePath).Length) / 1024.0 / 1024.0:F2} MB");
                        writer.WriteLine("");
                        writer.WriteLine("==========================================================================================");
                        writer.WriteLine(" FILE DETAILS");
                        writer.WriteLine("==========================================================================================");
                        writer.WriteLine("");

                        foreach (var res in results)
                        {
                            string status = res.Success ? "[OK]" : "[FAIL]";
                            writer.WriteLine($" {status} {res.Filename}");
                            writer.WriteLine($"      Source : {res.SourcePath}");
                            writer.WriteLine($"      Dest   : {res.DestPath}");
                            writer.WriteLine($"      Hash   : {res.SourceHash} (xxHash64)");
                            if (!res.Success) writer.WriteLine($"      ERROR  : {res.Note}");
                            writer.WriteLine("------------------------------------------------------------------------------------------");
                        }
                        
                        writer.WriteLine("");
                        writer.WriteLine(" END OF REPORT");
                        writer.WriteLine("==========================================================================================");
                    }
                }
                catch { /* Report generation failed - non-critical but annoying */ }
            });
        }

        private void VerifyWritePermission(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) throw new ArgumentNullException(nameof(folderPath));
            
            string dummyPath = Path.Combine(folderPath, $".veriflow_check_{Guid.NewGuid()}.tmp");
            using (File.Create(dummyPath, 1, FileOptions.DeleteOnClose)) { }
        }
    }

    public class CopyResult
    {
        public bool Success { get; set; }
        public string Filename { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestPath { get; set; } = string.Empty;
        public string SourceHash { get; set; } = string.Empty;
        public string DestHash { get; set; } = string.Empty;
        public double AverageSpeed { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class CopyProgress
    {
        public double Percentage { get; set; }
        public double TransferSpeedMbPerSec { get; set; }
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public string Status { get; set; } = "Copying";
    }
}
