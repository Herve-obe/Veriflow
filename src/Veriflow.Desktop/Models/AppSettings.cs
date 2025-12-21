using System;
using System.IO;

namespace Veriflow.Desktop.Models
{
    /// <summary>
    /// Application settings model
    /// </summary>
    public class AppSettings
    {
        // General Settings
        public bool EnableAutoSave { get; set; } = false;
        public bool ShowConfirmationDialogs { get; set; } = true;

        // Session Settings
        public string DefaultSessionFolder { get; set; } = GetDefaultSessionFolder();

        // Window State
        public double WindowWidth { get; set; } = 1280;
        public double WindowHeight { get; set; } = 720;
        public bool WindowMaximized { get; set; } = true; // Default to maximized on first run

        // Theme Settings (for future use)
        public string ThemeName { get; set; } = "Dark";

        private static string GetDefaultSessionFolder()
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "Veriflow", "Sessions");
        }

        /// <summary>
        /// Creates a copy of the current settings
        /// </summary>
        public AppSettings Clone()
        {
            return new AppSettings
            {
                EnableAutoSave = this.EnableAutoSave,
                ShowConfirmationDialogs = this.ShowConfirmationDialogs,
                DefaultSessionFolder = this.DefaultSessionFolder,
                WindowWidth = this.WindowWidth,
                WindowHeight = this.WindowHeight,
                WindowMaximized = this.WindowMaximized,
                ThemeName = this.ThemeName
            };
        }
    }
}
