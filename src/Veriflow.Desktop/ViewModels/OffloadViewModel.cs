using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Veriflow.Desktop.Services;

namespace Veriflow.Desktop.ViewModels
{
    public partial class OffloadViewModel : ObservableObject
    {
        // --- SERVICES & TOKENS ---
        private readonly SecureCopyService _secureCopyService;
        private CancellationTokenSource? _cts;

        // Transaction Tracking
        private List<string> _copiedFiles = new();
        private List<string> _createdDirectories = new();

        // --- PROPERTIES ---

        // FIX 1: REMOVE ATTRIBUTES & IMPLEMENT MANUAL NOTIFICATION
        [ObservableProperty]
        private bool _isBusy;

        partial void OnIsBusyChanged(bool value)
        {
            // Explicitly force updates on the UI thread
            UpdateUI(() =>
            {
                CancelCommand.NotifyCanExecuteChanged();
                ToggleCopyCommand.NotifyCanExecuteChanged();
                StartOffloadCommand.NotifyCanExecuteChanged();

                // Refresh interactions
                PickSourceCommand.NotifyCanExecuteChanged();
                PickDest1Command.NotifyCanExecuteChanged();
                PickDest2Command.NotifyCanExecuteChanged();
                DropSourceCommand.NotifyCanExecuteChanged();
                DropDest1Command.NotifyCanExecuteChanged();
                DropDest1Command.NotifyCanExecuteChanged();
                DropDest2Command.NotifyCanExecuteChanged();
                
                // Clear/Reset Commands
                ClearSourceCommand.NotifyCanExecuteChanged();
                ClearDest1Command.NotifyCanExecuteChanged();
                ClearDest2Command.NotifyCanExecuteChanged();
                ResetAllCommand.NotifyCanExecuteChanged();
            });
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleCopyCommand))]
        private bool _isCancelling;

        [ObservableProperty] private double _progressValue;
        [ObservableProperty] private string _timeRemainingDisplay = "--:--";
        [ObservableProperty] private string _currentSpeedDisplay = "0 MB/s";
        [ObservableProperty] private string _currentHashDisplay = "xxHash64: -";
        [ObservableProperty] private string? _logText;
        [ObservableProperty] private int _filesCopiedCount;
        [ObservableProperty] private int _errorsCount;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartOffloadCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleCopyCommand))]
        private string? _sourcePath;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartOffloadCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleCopyCommand))]
        private string? _destination1Path;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartOffloadCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleCopyCommand))]
        private string? _destination2Path;

        // --- CONSTRUCTOR ---
        public OffloadViewModel()
        {
            _secureCopyService = new SecureCopyService();
        }

        // --- COMMANDS ---

        private bool CanStart()
        {
            return !IsBusy && 
                   !string.IsNullOrEmpty(SourcePath) && 
                   (!string.IsNullOrEmpty(Destination1Path) || !string.IsNullOrEmpty(Destination2Path));
        }

        private bool CanCancel()
        {
            return IsBusy && !IsCancelling;
        }

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task StartOffload()
        {
            UpdateUI(() =>
            {
                IsBusy = true; // Triggers OnIsBusyChanged
                IsCancelling = false;

                _copiedFiles.Clear();
                _createdDirectories.Clear();

                ProgressValue = 0;
                LogText = "Initialisation...";
                FilesCopiedCount = 0;
                ErrorsCount = 0;
                CurrentSpeedDisplay = "0 MB/s";
                CurrentHashDisplay = "xxHash64: -";
                TimeRemainingDisplay = "--:--";

                _cts = new CancellationTokenSource();

                Console.WriteLine("--- 1. START: IsBusy set, trying to enable Cancel button ---");
            });

            try
            {
                await Task.Run(async () => await ProcessCopySequence(_cts!.Token));
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("--- 4. CANCELLATION CAUGHT: Starting Rollback ---");
                await PerformRollback();
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    LogText = $"Erreur : {ex.Message}";
                    IsBusy = false;
                    MessageBox.Show($"Erreur : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _cts?.Dispose();
                    _cts = null;

                    StartOffloadCommand.NotifyCanExecuteChanged();
                    CancelCommand.NotifyCanExecuteChanged();
                    ToggleCopyCommand.NotifyCanExecuteChanged();
                });
            }
        }

        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void Cancel()
        {
            if (_cts != null && !IsCancelling)
            {
                UpdateUI(() => 
                {
                    LogText = "🛑 Arrêt demandé... Nettoyage imminent.";
                    IsCancelling = true; 
                    CancelCommand.NotifyCanExecuteChanged(); 
                });
                
                try 
                { 
                    Console.WriteLine("--- 3. CANCEL REQUESTED: Sending token signal ---");
                    _cts.Cancel(); 
                } catch { }
            }
        }

        [RelayCommand(CanExecute = nameof(CanToggleCopy))]
        private void ToggleCopy()
        {
            if (IsBusy)
            {
                if (CancelCommand.CanExecute(null)) Cancel();
            }
            else
            {
                // Fire and forget, StartOffload handles its own exceptions
                if (StartOffloadCommand.CanExecute(null)) _ = StartOffload();
            }
        }

        private bool CanToggleCopy()
        {
            if (IsBusy) return CanCancel();
            return CanStart();
        }

        // --- CORE LOGIC ---

        private async Task ProcessCopySequence(CancellationToken ct)
        {
            UpdateUI(() => LogText = "Analyse des fichiers en cours...");

            var scannedFiles = new List<FileInfo>();
            if (Directory.Exists(SourcePath))
            {
                ScanDirectoryRecursive(new DirectoryInfo(SourcePath!), scannedFiles, 0, 3, ct);
            }

            if (scannedFiles.Count == 0)
            {
                UpdateUI(() => {
                    LogText = "Aucun fichier trouvé (limite profondeur: 3).";
                    IsBusy = false;
                });
                return;
            }

            long totalBytesToCopy = scannedFiles.Sum(f => f.Length);
            long totalBytesTransferred = 0;
            var sourceDir = new DirectoryInfo(SourcePath!);

            UpdateUI(() => LogText = $"{scannedFiles.Count} fichiers à copier ({totalBytesToCopy / 1024 / 1024} MB)");

            Console.WriteLine($"--- 2. COPY THREAD STARTED: Found {scannedFiles.Count} files ---");

            foreach (var file in scannedFiles)
            {
                ct.ThrowIfCancellationRequested(); 

                UpdateUI(() => LogText = $"Securing {file.Name}...");
                
                long initialBytesTransferred = totalBytesTransferred;
                string relativePath = Path.GetRelativePath(sourceDir.FullName, file.FullName);

                // Throttle UI updates to prevent flooding (max 20 per second)
                var lastUpdate = DateTime.MinValue;

                var progress = new Progress<CopyProgress>(p =>
                {
                    // FIX 2: Crash on Progress Update (Null Check)
                    if (Application.Current == null) return;

                    var now = DateTime.UtcNow;
                    // Always update if complete (100%), otherwise throttle to 50ms
                    if ((now - lastUpdate).TotalMilliseconds < 50 && p.Percentage < 100) return;
                    lastUpdate = now;

                    // FIX 3: Use InvokeAsync to prevent background thread from locking if UI is busy
                    Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        CurrentSpeedDisplay = $"{p.TransferSpeedMbPerSec:F1} MB/s";
                        long currentFileBytes = p.BytesTransferred;
                        long globalBytes = initialBytesTransferred + currentFileBytes;
                        
                        if (totalBytesToCopy > 0)
                        {
                            ProgressValue = (double)globalBytes / totalBytesToCopy * 100;
                            
                            double speedBytesPerSec = p.TransferSpeedMbPerSec * 1024 * 1024;
                            if (speedBytesPerSec > 0)
                            {
                                double secondsRemaining = (totalBytesToCopy - globalBytes) / speedBytesPerSec;
                                TimeSpan timeSpan = TimeSpan.FromSeconds(secondsRemaining);
                                TimeRemainingDisplay = timeSpan.ToString(timeSpan.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss");
                            }
                        }
                    });
                });

                var destPaths = new List<string>();
                if (!string.IsNullOrEmpty(Destination1Path)) destPaths.Add(Path.Combine(Destination1Path, relativePath));
                if (!string.IsNullOrEmpty(Destination2Path)) destPaths.Add(Path.Combine(Destination2Path, relativePath));

                // Pre-Create Directories
                foreach (var destFile in destPaths)
                {
                     var parentDir = Path.GetDirectoryName(destFile);
                     if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                     {
                         Directory.CreateDirectory(parentDir);
                         if (!_createdDirectories.Contains(parentDir)) _createdDirectories.Add(parentDir);
                     }
                }

                CopyResult? result = null;

                foreach (var destFile in destPaths)
                {
                    ct.ThrowIfCancellationRequested();
                    var copyRes = await _secureCopyService.CopyFileSecureAsync(file.FullName, destFile, progress, ct);
                    if (copyRes.Success) 
                    {
                        _copiedFiles.Add(destFile);
                        if (result == null) result = copyRes;
                    }
                }

                UpdateUI(() =>
                {
                    if (result != null && result.Success) FilesCopiedCount++;
                    else ErrorsCount++;
                });

                totalBytesTransferred += file.Length;
            }
            
            UpdateUI(() => 
            {
                IsBusy = false;
                MessageBox.Show($"Terminé ! {FilesCopiedCount} fichiers copiés.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private async Task PerformRollback()
        {
            Console.WriteLine("--- 5. ROLLBACK INITIATED: Deleting files... ---");

            UpdateUI(() =>
            {
                LogText = "❌ Annulation... Nettoyage des fichiers...";
                TimeRemainingDisplay = "ROLLBACK";
                CurrentSpeedDisplay = "";
                CurrentHashDisplay = "Cleaning Up...";
            });
            
            await Task.Run(() =>
            {
                foreach (var file in _copiedFiles)
                {
                    try { if (File.Exists(file)) File.Delete(file); } catch { }
                }

                for (int i = _createdDirectories.Count - 1; i >= 0; i--)
                {
                    var dir = _createdDirectories[i];
                    try
                    {
                        if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
                    }
                    catch { }
                }
            });

            UpdateUI(() =>
            {
                IsCancelling = false;
                IsBusy = false;
                
                ProgressValue = 0;
                LogText = "Prêt.";
                TimeRemainingDisplay = "--:--";
                CurrentHashDisplay = "xxHash64: -";
                CurrentSpeedDisplay = "0 MB/s";
                
                MessageBox.Show("La copie a été annulée. Tous les fichiers copiés ont été nettoyés.", "Annulation Confirmée", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void ScanDirectoryRecursive(DirectoryInfo dir, List<FileInfo> fileList, int currentDepth, int maxDepth, CancellationToken ct)
        {
            if (currentDepth > maxDepth) return;
            if (ct.IsCancellationRequested) return;

            var excludedFolders = new[] { ".git", "$RECYCLE.BIN", "System Volume Information", "AppData", "node_modules" };
            if (excludedFolders.Contains(dir.Name, StringComparer.OrdinalIgnoreCase)) return;

            try
            {
                fileList.AddRange(dir.GetFiles());
                foreach (var subDir in dir.GetDirectories()) ScanDirectoryRecursive(subDir, fileList, currentDepth + 1, maxDepth, ct);
            }
            catch (Exception) { }
        }

        private void UpdateUI(Action action)
        {
            if (Application.Current == null) return;
            Application.Current.Dispatcher.Invoke(action);
        }

        // --- DRAG & DROP & PICKERS ---
        private bool CanInteract() => !IsBusy;

        [RelayCommand(CanExecute = nameof(CanInteract))] private void PickSource() { var p = PickFolder(); if (p != null) SourcePath = p; }
        [RelayCommand(CanExecute = nameof(CanInteract))] private void PickDest1() { var p = PickFolder(); if (p != null) Destination1Path = p; }
        [RelayCommand(CanExecute = nameof(CanInteract))] private void PickDest2() { var p = PickFolder(); if (p != null) Destination2Path = p; }

        [RelayCommand(CanExecute = nameof(CanInteract))] private void DropSource(DragEventArgs e) => HandleDrop(e, p => SourcePath = p);
        [RelayCommand(CanExecute = nameof(CanInteract))] private void DropDest1(DragEventArgs e) => HandleDrop(e, p => Destination1Path = p);
        [RelayCommand(CanExecute = nameof(CanInteract))] private void DropDest2(DragEventArgs e) => HandleDrop(e, p => Destination2Path = p);
        [RelayCommand] private void DragOver(DragEventArgs e) { e.Effects = DragDropEffects.Copy; e.Handled = true; }

        // --- CLEAR & RESET COMMANDS ---
        [RelayCommand(CanExecute = nameof(CanInteract))] private void ClearSource() => SourcePath = null;
        [RelayCommand(CanExecute = nameof(CanInteract))] private void ClearDest1() => Destination1Path = null;
        [RelayCommand(CanExecute = nameof(CanInteract))] private void ClearDest2() => Destination2Path = null;

        [RelayCommand(CanExecute = nameof(CanInteract))]
        private void ResetAll()
        {
            SourcePath = null;
            Destination1Path = null;
            Destination2Path = null;
            LogText = "Prêt.";
            ProgressValue = 0;
            TimeRemainingDisplay = "--:--";
            CurrentSpeedDisplay = "0 MB/s";
            CurrentHashDisplay = "xxHash64: -";
        }

        private void HandleDrop(DragEventArgs e, Action<string> setPath)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    string path = files[0];
                    if (Directory.Exists(path)) setPath(path);
                    else if (File.Exists(path)) setPath(Path.GetDirectoryName(path)!);
                }
            }
        }

        private string? PickFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Sélectionner un dossier",
                Multiselect = false
            };
            return (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName)) ? dialog.FolderName : null;
        }
    }
}