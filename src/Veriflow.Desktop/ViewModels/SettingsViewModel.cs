using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using Veriflow.Desktop.Models;
using Veriflow.Desktop.Services;

namespace Veriflow.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel for Settings Window
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private AppSettings _originalSettings;

        [ObservableProperty]
        private bool _enableAutoSave;

        [ObservableProperty]
        private int _autoSaveIntervalMinutes = 5;
        
        partial void OnAutoSaveIntervalMinutesChanged(int value)
        {
            // Validate range (1-60 minutes)
            if (value < 1) AutoSaveIntervalMinutes = 1;
            if (value > 60) AutoSaveIntervalMinutes = 60;
        }

        [ObservableProperty]
        private bool _showConfirmationDialogs;

        [ObservableProperty]
        private string _defaultSessionFolder = string.Empty;

        public SettingsViewModel()
        {
            _settingsService = SettingsService.Instance;
            _originalSettings = _settingsService.GetSettings();
            
            // Load current settings
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = _settingsService.GetSettings();
            EnableAutoSave = settings.EnableAutoSave;
            AutoSaveIntervalMinutes = settings.AutoSaveIntervalMinutes;
            ShowConfirmationDialogs = settings.ShowConfirmationDialogs;
            DefaultSessionFolder = settings.DefaultSessionFolder;
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Default Session Folder",
                InitialDirectory = DefaultSessionFolder
            };

            if (dialog.ShowDialog() == true)
            {
                DefaultSessionFolder = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void Save()
        {
            var settings = _settingsService.GetSettings();
            settings.EnableAutoSave = EnableAutoSave;
            settings.AutoSaveIntervalMinutes = AutoSaveIntervalMinutes;
            settings.ShowConfirmationDialogs = ShowConfirmationDialogs;
            settings.DefaultSessionFolder = DefaultSessionFolder;

            _settingsService.SaveSettings(settings);
        }

        [RelayCommand]
        private void Cancel()
        {
            // Reload original settings (discard changes)
            LoadSettings();
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            _settingsService.ResetToDefaults();
            LoadSettings();
        }

        /// <summary>
        /// Checks if settings have been modified
        /// </summary>
        public bool HasChanges()
        {
            var current = _settingsService.GetSettings();
            return EnableAutoSave != current.EnableAutoSave ||
                   ShowConfirmationDialogs != current.ShowConfirmationDialogs ||
                   DefaultSessionFolder != current.DefaultSessionFolder;
        }
    }
}
