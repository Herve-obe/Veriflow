using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Veriflow.Desktop.Services;
using Veriflow.Desktop.Helpers;

namespace Veriflow.Desktop.ViewModels
{
    public partial class SecureCopyViewModel : ObservableObject
    {
        // --- SERVICES & TOKENS ---
        private readonly SecureCopyService _secureCopyService;
        private CancellationTokenSource? _cts;

        // Transaction Tracking
        private List<string> _copiedFiles = new();
        private List<string> _createdDirectories = new();

        // --- PROPERTIES ---

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOffloadMode))]
        private bool _isVerifyMode;

        public bool IsOffloadMode => !IsVerifyMode;

        [RelayCommand] private void SwitchToOffload() => IsVerifyMode = false;
        [RelayCommand] private void SwitchToVerify() => IsVerifyMode = true;


        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartVerifyCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResetAllCommand))]
        private string? _verifyPath;

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
        [NotifyCanExecuteChangedFor(nameof(ResetAllCommand))]
        private string? _sourcePath;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartOffloadCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleCopyCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResetAllCommand))]
        private string? _destination1Path;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartOffloadCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleCopyCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResetAllCommand))]
        private string? _destination2Path;

        // --- CONSTRUCTOR ---
        public SecureCopyViewModel()
        {
            _secureCopyService = new SecureCopyService();
            InitializeExplorer();
        }

        // --- COMMANDS ---

        private bool CanVerify() => !IsBusy && !string.IsNullOrEmpty(VerifyPath) && Directory.Exists(VerifyPath);

        [RelayCommand(CanExecute = nameof(CanVerify))]
        private async Task StartVerify()
        {
            if (!CanVerify()) return;

            IsBusy = true;
            IsCancelling = false;
            _cts = new CancellationTokenSource();
            FilesCopiedCount = 0;
            ErrorsCount = 0;
            LogText = "Starting Verification...";
            ProgressValue = 0;
            TimeRemainingDisplay = "Calculating...";
            CurrentHashDisplay = "xxHash64: -";

            try
            {
                var mhlService = new MhlService();
                var progress = new Progress<CopyProgress>(p =>
                {
                    ProgressValue = p.Percentage;
                    CurrentSpeedDisplay = $"{p.TransferSpeedMbPerSec:F1} MB/s";
                    TimeRemainingDisplay = "Verifying..."; 
                    // Could add proper time calc here but simplistic for now
                    LogText = p.Status;
                });

                var results = await mhlService.VerifyMhlAsync(VerifyPath!, progress, _cts.Token);
                
                // Process Results
                int successCount = results.Count(r => r.Success);
                int failCount = results.Count(r => !r.Success);
                FilesCopiedCount = successCount;
                ErrorsCount = failCount;

                UpdateUI(() =>
                {
                    IsBusy = false;
                    ProgressValue = 100;
                    TimeRemainingDisplay = "00:00";
                    
                    if (failCount == 0 && successCount > 0)
                    {
                        LogText = "Verification Complete. All OK.";
                        CurrentHashDisplay = "VERIFIED";
                        ProMessageBox.Show($"Verification Success!\n\n{successCount} files verified matching MHL records.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (successCount == 0 && failCount == 0)
                    {
                         LogText = "No MHL or Files found.";
                         ProMessageBox.Show("No MHL file found or no files matched.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        LogText = "Verification FAILED.";
                        CurrentHashDisplay = "ERRORS FOUND";
                        ProMessageBox.Show($"Verification Completed with ERRORS.\n\nMatches: {successCount}\nMismatches/Missing: {failCount}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                UpdateUI(() => LogText = "Verification Cancelled.");
            }
            catch (Exception ex)
            {
                UpdateUI(() => 
                {
                    LogText = $"Error: {ex.Message}";
                    ErrorsCount++;
                });
            }
            finally
            {
                IsBusy = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

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
                LogText = "Initializing...";
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
                Console.WriteLine("--- 4. CANCELLATION CAUGHT: User Aborted ---");
                UpdateUI(() =>
                {
                    IsCancelling = false;
                    IsBusy = false;
                    ProgressValue = 0;
                    LogText = "Cancelled.";
                    TimeRemainingDisplay = "--:--";
                    CurrentHashDisplay = "xxHash64: -";
                    CurrentSpeedDisplay = "0 MB/s";
                    
                    ProMessageBox.Show("SECURE COPY annulée par l'utilisateur.\nAttention, suite à l'annulation du processus de copie, certains fichiers copiés sont peut-être partiels ou corrompus.", "Annulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    LogText = $"Error: {ex.Message}";
                    IsBusy = false;
                    ProMessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    LogText = "🛑 Stop requested... Cleanup imminent.";
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
            UpdateUI(() => LogText = "Analyzing files...");

            var scannedFiles = new List<FileInfo>();
            if (Directory.Exists(SourcePath))
            {
                ScanDirectoryRecursive(new DirectoryInfo(SourcePath!), scannedFiles, 0, 5, ct); 
            }

            if (scannedFiles.Count == 0)
            {
                UpdateUI(() => {
                    LogText = "No files found (depth limit: 5).";
                    IsBusy = false;
                });
                return;
            }

            long totalBytesToCopy = scannedFiles.Sum(f => f.Length);
            long totalBytesTransferred = 0;
            var sourceDir = new DirectoryInfo(SourcePath!);

            int destCount = 0;
            if (!string.IsNullOrEmpty(Destination1Path)) destCount++;
            if (!string.IsNullOrEmpty(Destination2Path)) destCount++;

            UpdateUI(() => LogText = $"{scannedFiles.Count} files to copy to {destCount} destination(s) ({totalBytesToCopy / 1024 / 1024:F1} MB)");

            var sessionResults1 = new List<CopyResult>();
            var sessionResults2 = new List<CopyResult>();

            foreach (var file in scannedFiles)
            {
                ct.ThrowIfCancellationRequested(); 

                string relativePath = Path.GetRelativePath(sourceDir.FullName, file.FullName);
                long initialBytesTransferred = totalBytesTransferred;

                // PREPARE DESTINATIONS
                var destPaths = new List<string>();
                var mapIndexToSession = new Dictionary<int, List<CopyResult>>();

                if (!string.IsNullOrEmpty(Destination1Path)) 
                {
                    string fullDestPath = Path.Combine(Destination1Path, relativePath);
                    destPaths.Add(fullDestPath);
                    mapIndexToSession[destPaths.Count - 1] = sessionResults1;
                }
                
                if (!string.IsNullOrEmpty(Destination2Path))
                {
                    string fullDestPath = Path.Combine(Destination2Path, relativePath);
                    destPaths.Add(fullDestPath);
                    mapIndexToSession[destPaths.Count - 1] = sessionResults2;
                }

                // UI Update for START of file
                UpdateUI(() => 
                {
                    LogText = $"Securing: {file.Name}";
                    CurrentHashDisplay = "Hash Calc & Copy...";
                });

                var progress = new Progress<CopyProgress>(p =>
                {
                    if (Application.Current == null) return;
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

                // EXECUTE MULTI-DEST COPY
                // The service handles ALL writes sequentially reading the source ONCE.
                try
                {
                    var fileResults = await _secureCopyService.CopyFileMultiDestSecureAsync(file.FullName, destPaths, progress, ct);
                    
                    // Dispatch Results
                    bool allSuccess = true;
                    for (int i = 0; i < fileResults.Count; i++)
                    {
                        var res = fileResults[i];
                        if (mapIndexToSession.ContainsKey(i))
                        {
                            mapIndexToSession[i].Add(res);
                            if (res.Success) _copiedFiles.Add(res.DestPath);
                            else allSuccess = false;
                        }
                    }

                    UpdateUI(() =>
                    {
                        // Simplified status update
                        if (allSuccess)
                        {
                            FilesCopiedCount++;
                            var shortHash = fileResults.Count > 0 ? fileResults[0].SourceHash.Substring(0, 8) : "-";
                            CurrentHashDisplay = $"Verif OK ({shortHash})";
                        }
                        else
                        {
                            ErrorsCount++;
                            CurrentHashDisplay = "COPY/VERIF ERROR";
                        }
                    });
                }
                catch (Exception ex)
                {
                    UpdateUI(() => 
                    {
                        LogText = $"Critical Error: {ex.Message}";
                        ErrorsCount++;
                    });
                }

                totalBytesTransferred += file.Length;
            }
            
            // --- GENERATE REPORTS ---
            UpdateUI(() => LogText = "Generating reports (TXT + MHL)...");
            
            if (!string.IsNullOrEmpty(Destination1Path) && sessionResults1.Any(r => r.Success))
            {
                await _secureCopyService.GenerateOffloadReportAsync(Destination1Path, sessionResults1);
                var mhlService = new MhlService();
                await mhlService.GenerateMhlAsync(Destination1Path, sessionResults1);
            }
            if (!string.IsNullOrEmpty(Destination2Path) && sessionResults2.Any(r => r.Success))
            {
                await _secureCopyService.GenerateOffloadReportAsync(Destination2Path, sessionResults2);
                var mhlService = new MhlService(); // New instance is fine, stateless
                await mhlService.GenerateMhlAsync(Destination2Path, sessionResults2);
            }

            UpdateUI(() => 
            {
                IsBusy = false;
                ProgressValue = 100;
                TimeRemainingDisplay = "00:00";
                LogText = "Done.";
                ProMessageBox.Show($"Offload complete!\n\nFiles copied: {FilesCopiedCount}\nErrors: {ErrorsCount}\n\nReports (TXT & MHL) generated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
        [RelayCommand(CanExecute = nameof(CanInteract))] private void ClearVerify() => VerifyPath = null;
        [RelayCommand(CanExecute = nameof(CanInteract))] private void PickVerify() { var p = PickFolder(); if (p != null) VerifyPath = p; }
        [RelayCommand(CanExecute = nameof(CanInteract))] private void DropVerify(DragEventArgs e) => HandleDrop(e, p => VerifyPath = p);

        private bool CanReset()
        {
            return CanInteract() && 
                   (!string.IsNullOrEmpty(SourcePath) || 
                    !string.IsNullOrEmpty(Destination1Path) || 
                    !string.IsNullOrEmpty(Destination2Path) ||
                    !string.IsNullOrEmpty(VerifyPath));
        }

        [RelayCommand(CanExecute = nameof(CanReset))]
        private void ResetAll()
        {
            SourcePath = null;
            Destination1Path = null;
            Destination2Path = null;
            VerifyPath = null;
            LogText = "Ready.";
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
                Title = "Select a folder",
                Multiselect = false
            };
            return (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName)) ? dialog.FolderName : null;
        }


        // --- EXPLORER INTEGRATION ---
        [ObservableProperty]
        private ObservableCollection<DriveViewModel> _drives = new();

        private void InitializeExplorer()
        {
            RefreshDrives();
            // Optional: Add timer if needed, skipping for brevity/robustness balance in this prompt
        }

        private void RefreshDrives()
        {
             var safeDriveList = new List<DriveViewModel>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                   if (drive.IsReady) safeDriveList.Add(new DriveViewModel(drive, LoadDirectoryFromExplorer));
                }
            }
            catch {}

            if (Application.Current.Dispatcher.CheckAccess()) UpdateDrivesCollection(safeDriveList);
            else Application.Current.Dispatcher.Invoke(() => UpdateDrivesCollection(safeDriveList));
        }

        private void UpdateDrivesCollection(List<DriveViewModel> newDrives)
        {
            Drives.Clear();
            foreach (var d in newDrives) Drives.Add(d);
        }

        private void LoadDirectoryFromExplorer(string path)
        {
            if (Directory.Exists(path))
            {
                if (IsVerifyMode) VerifyPath = path;
                else SourcePath = path;
            }
        }

    }
}