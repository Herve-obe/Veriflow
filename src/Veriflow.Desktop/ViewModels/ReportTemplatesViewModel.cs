using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using Veriflow.Core.Models;

namespace Veriflow.Desktop.ViewModels
{
    public partial class ReportTemplatesViewModel : ObservableObject
    {
        [ObservableProperty]
        private ReportSettings _settings;

        public ReportTemplatesViewModel(ReportSettings settings)
        {
            _settings = settings;
        }

        [RelayCommand]
        private void BrowseLogo()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Select Custom Logo"
            };

            if (dialog.ShowDialog() == true)
            {
                Settings.CustomLogoPath = dialog.FileName;
                Settings.UseCustomLogo = true;
            }
        }

        [RelayCommand]
        private void ClearLogo()
        {
            Settings.CustomLogoPath = "";
            Settings.UseCustomLogo = false;
        }

        [RelayCommand]
        private void SavePreset()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Veriflow Template (*.vftemplate)|*.vftemplate",
                Title = "Save Report Template"
            };

            if (dialog.ShowDialog() == true)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(Settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show("Template saved successfully.", "Templates", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void LoadPreset()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Veriflow Template (*.vftemplate)|*.vftemplate",
                Title = "Load Report Template"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var loadedSettings = System.Text.Json.JsonSerializer.Deserialize<ReportSettings>(json);
                    
                    if (loadedSettings != null)
                    {
                        // Update properties one by one to trigger UI updates
                        // Ideally we would replace the whole object but binding might break if not handled carefully
                        // Or utilize a CopyFrom method. For now, manual mapping or property reflection.
                        
                        Settings.CustomLogoPath = loadedSettings.CustomLogoPath;
                        Settings.UseCustomLogo = loadedSettings.UseCustomLogo;
                        Settings.CustomTitle = loadedSettings.CustomTitle;
                        Settings.UseCustomTitle = loadedSettings.UseCustomTitle;
                        
                        Settings.ShowFilename = loadedSettings.ShowFilename;
                        Settings.ShowScene = loadedSettings.ShowScene;
                        Settings.ShowTake = loadedSettings.ShowTake;
                        Settings.ShowTimecode = loadedSettings.ShowTimecode;
                        Settings.ShowDuration = loadedSettings.ShowDuration;
                        Settings.ShowNotes = loadedSettings.ShowNotes;
                        
                        Settings.ShowFps = loadedSettings.ShowFps;
                        Settings.ShowIso = loadedSettings.ShowIso;
                        Settings.ShowWhiteBalance = loadedSettings.ShowWhiteBalance;
                        Settings.ShowCodecResultion = loadedSettings.ShowCodecResultion;
                        
                        Settings.ShowSampleRate = loadedSettings.ShowSampleRate;
                        Settings.ShowBitDepth = loadedSettings.ShowBitDepth;
                        Settings.ShowTracks = loadedSettings.ShowTracks;
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Error loading template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        [RelayCommand]
        private void Close(Window window)
        {
            window?.Close();
        }
    }
}
