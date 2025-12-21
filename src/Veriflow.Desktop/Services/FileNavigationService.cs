using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Veriflow.Desktop.Services
{
    /// <summary>
    /// Service for managing file navigation in directories.
    /// Provides Previous/Next file navigation functionality.
    /// </summary>
    public class FileNavigationService
    {
        /// <summary>
        /// Gets all files in the same directory as the current file that match the specified extensions.
        /// </summary>
        /// <param name="currentPath">Current file path</param>
        /// <param name="extensions">Array of file extensions (e.g., [".wav", ".mp3"])</param>
        /// <returns>Tuple containing the list of files and the current file index</returns>
        public (List<string> files, int currentIndex) GetSiblingFiles(string currentPath, string[] extensions)
        {
            try
            {
                var directory = Path.GetDirectoryName(currentPath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return (new List<string>(), -1);
                }

                var files = Directory.GetFiles(directory)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var currentIndex = files.FindIndex(f => f.Equals(currentPath, StringComparison.OrdinalIgnoreCase));

                return (files, currentIndex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSiblingFiles error: {ex.Message}");
                return (new List<string>(), -1);
            }
        }

        /// <summary>
        /// Gets the previous file in the list.
        /// </summary>
        /// <param name="files">List of files</param>
        /// <param name="currentIndex">Current file index</param>
        /// <returns>Previous file path or null if at the beginning</returns>
        public string? GetPreviousFile(List<string> files, int currentIndex)
        {
            if (currentIndex > 0 && currentIndex < files.Count)
            {
                return files[currentIndex - 1];
            }
            return null;
        }

        /// <summary>
        /// Gets the next file in the list.
        /// </summary>
        /// <param name="files">List of files</param>
        /// <param name="currentIndex">Current file index</param>
        /// <returns>Next file path or null if at the end</returns>
        public string? GetNextFile(List<string> files, int currentIndex)
        {
            if (currentIndex >= 0 && currentIndex < files.Count - 1)
            {
                return files[currentIndex + 1];
            }
            return null;
        }

        /// <summary>
        /// Checks if navigation to previous file is possible.
        /// </summary>
        public bool CanNavigatePrevious(int currentIndex) => currentIndex > 0;

        /// <summary>
        /// Checks if navigation to next file is possible.
        /// </summary>
        public bool CanNavigateNext(int currentIndex, int fileCount) => currentIndex >= 0 && currentIndex < fileCount - 1;
    }
}
