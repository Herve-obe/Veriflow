using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace SoundLogPro.Desktop.ViewModels
{
    public partial class OffloadViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PickSourceCommand))]
        [NotifyCanExecuteChangedFor(nameof(PickDest1Command))]
        [NotifyCanExecuteChangedFor(nameof(PickDest2Command))]
        [NotifyCanExecuteChangedFor(nameof(StartCopyCommand))]
        private bool _isBusy;

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private string? _sourcePath;

        [ObservableProperty]
        private string? _destination1Path;

        [ObservableProperty]
        private string? _destination2Path;

        [ObservableProperty]
        private string? _logText;

        public OffloadViewModel()
        {
        }

        [RelayCommand(CanExecute = nameof(CanInteract))]
        private void PickSource()
        {
            var path = PickFolder();
            if (!string.IsNullOrEmpty(path))
            {
                SourcePath = path;
                StartCopyCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanInteract))]
        private void PickDest1()
        {
            var path = PickFolder();
            if (!string.IsNullOrEmpty(path))
            {
                Destination1Path = path;
                StartCopyCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanInteract))]
        private void PickDest2()
        {
            var path = PickFolder();
            if (!string.IsNullOrEmpty(path))
            {
                Destination2Path = path;
                StartCopyCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanInteract() => !IsBusy;

        private bool CanCopy() => !IsBusy && !string.IsNullOrEmpty(SourcePath) && (!string.IsNullOrEmpty(Destination1Path) || !string.IsNullOrEmpty(Destination2Path));

        [RelayCommand(CanExecute = nameof(CanCopy))]
        private async Task StartCopy()
        {
            IsBusy = true;
            ProgressValue = 0;
            LogText = "Initialisation...";

            try
            {
                var sourceDir = new DirectoryInfo(SourcePath!);
                var files = sourceDir.GetFiles("*", SearchOption.AllDirectories);
                int totalFiles = files.Length;
                int processedFiles = 0;

                if (totalFiles == 0)
                {
                    LogText = "Aucun fichier à copier.";
                    MessageBox.Show("Le dossier source est vide.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                foreach (var file in files)
                {
                    processedFiles++;
                    LogText = $"Copie de {file.Name}...";

                    // Calculate relative path to keep structure
                    string relativePath = Path.GetRelativePath(sourceDir.FullName, file.FullName);

                    // Copy to Dest 1
                    if (!string.IsNullOrEmpty(Destination1Path))
                    {
                        var destFile = Path.Combine(Destination1Path, relativePath);
                        await CopyFileAsync(file.FullName, destFile);
                    }

                    // Copy to Dest 2
                    if (!string.IsNullOrEmpty(Destination2Path))
                    {
                        var destFile = Path.Combine(Destination2Path, relativePath);
                        await CopyFileAsync(file.FullName, destFile);
                    }

                    ProgressValue = (double)processedFiles / totalFiles * 100;
                }

                LogText = "Copie terminée avec succès !";
                MessageBox.Show("Sauvegarde terminée avec succès.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogText = $"Erreur : {ex.Message}";
                MessageBox.Show($"Une erreur est survenue : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CopyFileAsync(string source, string destination)
        {
            var directory = Path.GetDirectoryName(destination);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await sourceStream.CopyToAsync(destStream);
            }
        }

        private string? PickFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Sélectionner un dossier",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FolderName;
            }

            return null;
        }
    }
}
