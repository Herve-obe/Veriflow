using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;

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
        
        [ObservableProperty]
        private int _filesCopiedCount;
        
        [ObservableProperty]
        private int _errorsCount;

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
            FilesCopiedCount = 0;
            ErrorsCount = 0;
            var reportBuilder = new StringBuilder();
            var processingDate = DateTime.Now;

            reportBuilder.AppendLine("==========================================");
            reportBuilder.AppendLine($"RAPPORT DE COPIE SOUNDLOG PRO - {processingDate}");
            reportBuilder.AppendLine("==========================================");
            reportBuilder.AppendLine($"Source      : {SourcePath}");
            reportBuilder.AppendLine($"Destination 1: {Destination1Path ?? "N/A"}");
            reportBuilder.AppendLine($"Destination 2: {Destination2Path ?? "N/A"}");
            reportBuilder.AppendLine("------------------------------------------");
            reportBuilder.AppendLine("DÉTAIL DES FICHIERS :");

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
                    string status = "[OK]";
                    string errorDetail = "";

                    // Calculate relative path to keep structure
                    string relativePath = Path.GetRelativePath(sourceDir.FullName, file.FullName);

                    try 
                    {
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
                        
                        FilesCopiedCount++;
                    }
                    catch (Exception ex)
                    {
                        status = "[ERREUR]";
                        errorDetail = ex.Message;
                        ErrorsCount++;
                    }

                    reportBuilder.AppendLine($"{processingDate.ToShortTimeString()} - {relativePath} : {status} {errorDetail}");
                    ProgressValue = (double)processedFiles / totalFiles * 100;
                }

                // Finalize Report
                reportBuilder.AppendLine("------------------------------------------");
                reportBuilder.AppendLine($"RÉSUMÉ FINAL :");
                reportBuilder.AppendLine($"Fichiers traités : {totalFiles}");
                reportBuilder.AppendLine($"Succès           : {FilesCopiedCount}");
                reportBuilder.AppendLine($"Erreurs          : {ErrorsCount}");
                reportBuilder.AppendLine("==========================================");

                string reportContent = reportBuilder.ToString();
                string reportFileName = $"SoundLog_Report_{processingDate:yyyyMMdd_HHmmss}.txt";

                // Save Report to Destinations
                if (!string.IsNullOrEmpty(Destination1Path))
                    await File.WriteAllTextAsync(Path.Combine(Destination1Path, reportFileName), reportContent);
                
                if (!string.IsNullOrEmpty(Destination2Path))
                    await File.WriteAllTextAsync(Path.Combine(Destination2Path, reportFileName), reportContent);

                LogText = "Terminé.";

                if (ErrorsCount == 0)
                {
                    MessageBox.Show(
                        $"Succès ! {FilesCopiedCount} fichiers sécurisés.\nUn rapport a été généré dans le dossier de destination.", 
                        "Copie Terminée", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Attention ! {FilesCopiedCount} fichiers copiés, mais {ErrorsCount} erreurs détectées.\nVeuillez consulter le rapport pour plus de détails.", 
                        "Copie avec Erreurs", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogText = $"Erreur Critique : {ex.Message}";
                MessageBox.Show($"Une erreur critique est survenue : {ex.Message}", "Erreur Critique", MessageBoxButton.OK, MessageBoxImage.Error);
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
