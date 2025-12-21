using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Veriflow.Desktop.Services
{
    public class MhlService
    {
        /// <summary>
        /// Generates an MHL file (Media Hash List) for a given destination root.
        /// Adheres to MHL v1.1 Standard.
        /// </summary>
        /// <param name="destinationRoot">The absolute path to the destination folder.</param>
        /// <param name="results">List of copy results to include in the MHL.</param>
        /// <returns>The path to the created MHL file.</returns>
        public async Task<string> GenerateMhlAsync(string destinationRoot, List<CopyResult> results)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(destinationRoot) || !Directory.Exists(destinationRoot))
                   return string.Empty;

                // Filter only successful copies that belong to this destination tree
                // (Though usually the caller passes filtered list, we double check relative paths)
                var relevantResults = results.Where(r => r.Success && !string.IsNullOrEmpty(r.DestPath)).ToList();
                
                if (!relevantResults.Any()) return string.Empty;

                string mhlFilename = $"mask_{DateTime.Now:yyyy-MM-dd_HHmmss}.mhl";
                string mhlPath = Path.Combine(destinationRoot, mhlFilename);

                var doc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", "yes"),
                    new XElement("hashlist",
                        new XAttribute("version", "1.1"),
                        new XElement("creatorinfo",
                            new XElement("tool", "Veriflow"),
                            new XElement("toolversion", "1.1.0"),
                            new XElement("hostname", Environment.MachineName),
                            new XElement("username", Environment.UserName),
                            new XElement("startdate", relevantResults.Min(r => r.Timestamp).ToString("yyyy-MM-ddTHH:mm:sszzz")),
                            new XElement("finishdate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"))
                        ),
                        from res in relevantResults
                        select new XElement("hash",
                            new XElement("file", Path.GetRelativePath(destinationRoot, res.DestPath)),
                            new XElement("size", new FileInfo(res.SourcePath).Length), 
                            new XElement("xxhash64", res.SourceHash), // MHL standard supports xxhash64
                            new XElement("creationdate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")) 
                        )
                    )
                );

                // Use XmlWriter with indentation for readability
                var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }; // UTF-8 without BOM
                using (var writer = XmlWriter.Create(mhlPath, settings))
                {
                    doc.Save(writer);
                }

                return mhlPath;
            });
        }

        public async Task<List<CopyResult>> VerifyMhlAsync(string folderPath, IProgress<CopyProgress> progress, CancellationToken ct)
        {
            var results = new List<CopyResult>();

            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return results;

            // 1. Find latest MHL file
            var mhlFiles = Directory.GetFiles(folderPath, "*.mhl");
            if (mhlFiles.Length == 0) return results; // No MHL found

            // Sort by date desc, assume filename format mask_YYYY-MM-DD_HHmmss.mhl or similar
            // MHL standard allows any name, but typically we want the most recent one.
            var mhlPath = mhlFiles.OrderByDescending(f => File.GetCreationTime(f)).First();

            // 2. Parse MHL
            var doc = XDocument.Load(mhlPath);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None; // MHL usually has no namespace or standard one

            var hashes = doc.Descendants(ns + "hash").ToList();
            long totalBytes = 0;
            
            // Pre-calculate total bytes for progress
            foreach (var h in hashes)
            {
                if (long.TryParse(h.Element(ns + "size")?.Value, out long size)) totalBytes += size;
            }

            long currentBytes = 0;
            var startTime = DateTime.UtcNow;

            foreach (var h in hashes)
            {
                ct.ThrowIfCancellationRequested();

                var relativePath = h.Element(ns + "file")?.Value;
                var expectedHash = h.Element(ns + "xxhash64")?.Value;
                long size = 0;
                long.TryParse(h.Element(ns + "size")?.Value, out size);
                
                if (string.IsNullOrEmpty(relativePath)) continue;

                var fullPath = Path.Combine(folderPath, relativePath);
                var res = new CopyResult
                {
                    Filename = Path.GetFileName(fullPath),
                    SourcePath = "MHL Reference", // Indicating source is the MHL
                    DestPath = fullPath,
                    SourceHash = expectedHash ?? "",
                    Timestamp = DateTime.Now
                };

                if (!File.Exists(fullPath))
                {
                    res.Success = false;
                    res.Note = "File Missing";
                    results.Add(res);
                    continue;
                }

                // 3. Verify Hash (xxHash64)
                try
                {
                    string actualHash = await CalculateFileHashAsync(fullPath, size, progress, currentBytes, totalBytes, startTime, ct);
                    res.DestHash = actualHash;

                    if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        res.Success = true;
                        res.Note = "Verified (MHL)";
                    }
                    else
                    {
                        res.Success = false;
                        res.Note = "CHECKSUM MISMATCH";
                    }
                }
                catch (Exception ex)
                {
                    res.Success = false;
                    res.Note = $"Error: {ex.Message}";
                }
                
                results.Add(res);
                currentBytes += size;
            }

            return results;
        }

        private async Task<string> CalculateFileHashAsync(string path, long fileSize, IProgress<CopyProgress> progress, long processedGlobal, long totalGlobal, DateTime startTime, CancellationToken ct)
        {
             // Reusing implementation concept from SecureCopyService but strictly for reading
             var xxHash = new System.IO.Hashing.XxHash64();
             var buffer = new byte[1024 * 1024 * 4]; // 4MB
             using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024 * 4, true);
             
             int read;
             long processedLocal = 0;

             while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
             {
                 xxHash.Append(buffer.AsSpan(0, read));
                 processedLocal += read;
                 
                 // Report Progress
                 if (progress != null)
                 {
                     double totalRead = processedGlobal + processedLocal;
                     var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                     var speed = elapsed > 0 ? (totalRead / 1024.0 / 1024.0) / elapsed : 0;

                     progress.Report(new CopyProgress
                     {
                         Percentage = (double)totalRead / totalGlobal * 100,
                         TransferSpeedMbPerSec = speed,
                         BytesTransferred = (long)totalRead,
                         TotalBytes = totalGlobal,
                         Status = "Verifying..."
                     });
                 }
             }
             return BitConverter.ToString(xxHash.GetCurrentHash()).Replace("-", "").ToLowerInvariant();
        }
    }
}
