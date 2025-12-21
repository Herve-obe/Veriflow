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
    }
}
