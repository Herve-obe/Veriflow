using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Veriflow.Desktop.Models;

namespace Veriflow.Desktop.Services
{
    /// <summary>
    /// Service for managing recently opened files
    /// </summary>
    public class RecentFilesService
    {
        private static readonly Lazy<RecentFilesService> _instance = new(() => new RecentFilesService());
        private readonly string _recentFilesPath;
        private List<RecentFileEntry> _recentFiles = new();
        private readonly object _lock = new();
        private const int MaxRecentFiles = 10;

        public static RecentFilesService Instance => _instance.Value;

        public event EventHandler? RecentFilesChanged;

        private RecentFilesService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var veriflowFolder = Path.Combine(appDataPath, "Veriflow");
            
            Directory.CreateDirectory(veriflowFolder);
            
            _recentFilesPath = Path.Combine(veriflowFolder, "recent_files.json");
            LoadFromFile();
        }

        /// <summary>
        /// Adds a file to the recent files list
        /// </summary>
        public void AddRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            lock (_lock)
            {
                // Remove if already exists (to move to top)
                _recentFiles.RemoveAll(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

                // Add to top
                _recentFiles.Insert(0, new RecentFileEntry(filePath));

                // Limit to max files
                if (_recentFiles.Count > MaxRecentFiles)
                {
                    _recentFiles = _recentFiles.Take(MaxRecentFiles).ToList();
                }

                SaveToFile();
                RecentFilesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets the list of recent files
        /// </summary>
        public List<RecentFileEntry> GetRecentFiles()
        {
            lock (_lock)
            {
                // Filter out files that no longer exist
                _recentFiles = _recentFiles.Where(f => File.Exists(f.FilePath)).ToList();
                return new List<RecentFileEntry>(_recentFiles);
            }
        }

        /// <summary>
        /// Clears all recent files
        /// </summary>
        public void ClearRecentFiles()
        {
            lock (_lock)
            {
                _recentFiles.Clear();
                SaveToFile();
                RecentFilesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SaveToFile()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(_recentFiles, options);
                File.WriteAllText(_recentFilesPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save recent files: {ex.Message}");
            }
        }

        private void LoadFromFile()
        {
            try
            {
                if (!File.Exists(_recentFilesPath))
                    return;

                var json = File.ReadAllText(_recentFilesPath);
                var files = JsonSerializer.Deserialize<List<RecentFileEntry>>(json);
                
                if (files != null)
                {
                    // Filter out non-existent files on load
                    _recentFiles = files.Where(f => File.Exists(f.FilePath)).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load recent files: {ex.Message}");
                _recentFiles = new List<RecentFileEntry>();
            }
        }
    }
}
