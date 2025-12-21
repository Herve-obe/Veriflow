using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Veriflow.Desktop.Models; // Ensure this is present

namespace Veriflow.Desktop.ViewModels
{


    public enum AppMode { Audio, Video }
    public enum PageType { Media, Player, Sync, SecureCopy, Transcode, Reports }

    public partial class MainViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private string _title = "Veriflow Pro";

        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _applicationBackground = "#121212";

        [ObservableProperty]
        private AppMode _currentAppMode = AppMode.Video;

        partial void OnCurrentAppModeChanged(AppMode value)
        {
            _reportsViewModel.SetAppMode(value);
            _transcodeViewModel.SetAppMode(value);
            // Propagate to other VMs if needed
        }

        /// <summary>
        /// Automatically switches Audio/Video mode based on the file type of the first dragged file.
        /// This provides seamless UX when dragging files - no manual mode switching needed.
        /// </summary>
        public void AutoSwitchModeForFiles(string[] files)
        {
            if (files == null || files.Length == 0) return;

            // Check first file extension
            var ext = System.IO.Path.GetExtension(files[0]).ToLower();

            var audioExts = new[] { ".wav", ".mp3", ".aiff", ".flac", ".m4a" };
            var videoExts = new[] { ".mov", ".mp4", ".mxf", ".avi", ".mkv" };

            if (audioExts.Contains(ext) && CurrentAppMode != AppMode.Audio)
            {
                SwitchToAudioCommand.Execute(null);
            }
            else if (videoExts.Contains(ext) && CurrentAppMode != AppMode.Video)
            {
                SwitchToVideoCommand.Execute(null);
            }
        }

        [ObservableProperty]
        private PageType _currentPageType = PageType.Media;

        // ViewModels - Exposed as public for keyboard routing
        public SecureCopyViewModel SecureCopyViewModel { get; }
        public PlayerViewModel PlayerViewModel { get; }
        public AudioPlayerViewModel AudioViewModel { get; }
        public VideoPlayerViewModel VideoPlayerViewModel { get; }
        public TranscodeViewModel TranscodeViewModel { get; }
        public MediaViewModel MediaViewModel { get; }
        public SyncViewModel SyncViewModel { get; }
        public ReportsViewModel ReportsViewModel { get; }
        public SessionViewModel SessionViewModel { get; }

        // Private backing fields
        private readonly SecureCopyViewModel _secureCopyViewModel;
        private readonly PlayerViewModel _playerViewModel;
        private readonly AudioPlayerViewModel _audioViewModel;
        private readonly VideoPlayerViewModel _videoPlayerViewModel;
        private readonly TranscodeViewModel _transcodeViewModel;
        private readonly MediaViewModel _mediaViewModel;
        private readonly SyncViewModel _syncViewModel;
        private readonly ReportsViewModel _reportsViewModel;
        private readonly SessionViewModel _sessionViewModel;
        private readonly Services.CommandHistory _commandHistory = new();

        public ICommand ShowPlayerCommand { get; }
        public ICommand ShowMediaCommand { get; }
        public ICommand ShowTranscodeCommand { get; }
        public ICommand ShowSyncCommand { get; }
        public ICommand ShowSecureCopyCommand { get; }
        public ICommand ShowReportsCommand { get; }
        public ICommand SwitchToAudioCommand { get; }
        public ICommand SwitchToVideoCommand { get; }
        public ICommand ToggleAudioVideoModeCommand { get; }
        public ICommand OpenAboutCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand NewSessionCommand { get; }
        public ICommand OpenSessionCommand { get; }
        public ICommand SaveSessionCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        
        // Edit Menu Commands
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand CutCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand ClearCurrentPageCommand { get; }
        
        // Help Menu Commands
        public ICommand ViewHelpCommand { get; }
        public ICommand ShowKeyboardShortcutsCommand { get; }
        public ICommand OpenLogFolderCommand { get; }

        public MainViewModel()
        {
            // Initialize ViewModels
            _secureCopyViewModel = new();
            _playerViewModel = new();
            _audioViewModel = new();
            _videoPlayerViewModel = new();
            _transcodeViewModel = new();
            _mediaViewModel = new();
            _syncViewModel = new();
            _reportsViewModel = new();

            // Expose as public properties
            SecureCopyViewModel = _secureCopyViewModel;
            PlayerViewModel = _playerViewModel;
            AudioViewModel = _audioViewModel;
            VideoPlayerViewModel = _videoPlayerViewModel;
            TranscodeViewModel = _transcodeViewModel;
            MediaViewModel = _mediaViewModel;
            SyncViewModel = _syncViewModel;
            ReportsViewModel = _reportsViewModel;

            // Initialize SessionViewModel with callbacks
            _sessionViewModel = new SessionViewModel(
                captureStateCallback: CaptureSessionState,
                restoreStateCallback: RestoreSessionState);
            SessionViewModel = _sessionViewModel;

            // Navigation Commands
            ShowPlayerCommand = new RelayCommand(() => NavigateTo(PageType.Player));
            ShowMediaCommand = new RelayCommand(() => NavigateTo(PageType.Media));
            ShowTranscodeCommand = new RelayCommand(() => NavigateTo(PageType.Transcode));
            ShowSyncCommand = new RelayCommand(() => NavigateTo(PageType.Sync));
            ShowSecureCopyCommand = new RelayCommand(() => NavigateTo(PageType.SecureCopy));
            ShowReportsCommand = new RelayCommand(() => NavigateTo(PageType.Reports));

            SwitchToAudioCommand = new RelayCommand(() => SetMode(AppMode.Audio));
            SwitchToVideoCommand = new RelayCommand(() => SetMode(AppMode.Video));
            ToggleAudioVideoModeCommand = new RelayCommand(ToggleAudioVideoMode);
            OpenAboutCommand = new RelayCommand(OpenAbout);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

            // Session Commands
            NewSessionCommand = new RelayCommand(() => _sessionViewModel.NewSessionCommand.Execute(null));
            OpenSessionCommand = new RelayCommand(async () => await _sessionViewModel.OpenSessionCommand.ExecuteAsync(null));
            SaveSessionCommand = new RelayCommand(async () => await _sessionViewModel.SaveSessionCommand.ExecuteAsync(null));
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            
            // Edit Commands
            UndoCommand = new RelayCommand(Undo, CanUndo);
            RedoCommand = new RelayCommand(Redo, CanRedo);
            CutCommand = new RelayCommand(Cut, CanCutCopy);
            CopyCommand = new RelayCommand(Copy, CanCutCopy);
            PasteCommand = new RelayCommand(Paste, CanPaste);
            ClearCurrentPageCommand = new RelayCommand(ClearCurrentPage);
            
            // Help Commands
            ViewHelpCommand = new RelayCommand(ViewHelp);
            ShowKeyboardShortcutsCommand = new RelayCommand(ShowKeyboardShortcuts);
            OpenLogFolderCommand = new RelayCommand(OpenLogFolder);

            // Default
            SetMode(AppMode.Video);
            NavigateTo(PageType.SecureCopy); // Set start page to SECURE COPY

            // Navigation Wiring
            _mediaViewModel.RequestOpenInPlayer += async (path) =>
            {
                try
                {
                    if (CurrentAppMode == AppMode.Audio)
                    {
                         await _audioViewModel.LoadAudio(path);
                    }
                    else
                    {
                         await _videoPlayerViewModel.LoadVideo(path);
                    }
                    NavigateTo(PageType.Player); 
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error opening player: {ex.Message}");
                }
            };

            _mediaViewModel.RequestOffloadSource += (path) =>
            {
                _secureCopyViewModel.SourcePath = path;
                NavigateTo(PageType.SecureCopy);
            };

            _mediaViewModel.RequestTranscode += (files) =>
            {
                _transcodeViewModel.AddFiles(files);
                NavigateTo(PageType.Transcode);
            };

            _audioViewModel.RequestTranscode += (files) =>
            {
                _transcodeViewModel.AddFiles(files);
                NavigateTo(PageType.Transcode);
            };

            _videoPlayerViewModel.RequestTranscode += (files) =>
            {
                _transcodeViewModel.AddFiles(files);
                NavigateTo(PageType.Transcode);
            };

             _playerViewModel.RequestTranscode += (files) =>
            {
                _transcodeViewModel.AddFiles(files);
                NavigateTo(PageType.Transcode);
            };

            // Reports Integration
            _mediaViewModel.RequestCreateReport += (items, isVideo) =>
            {
               var type = isVideo ? ReportType.Video : ReportType.Audio;
               _reportsViewModel.CreateReport(items, type);
               // Update with specific context
               _mediaViewModel.SetReportStatus(isVideo, true);
               
               // Confirmation Popup
               string msg = isVideo ? "Camera Report created successfully." : "Sound Report created successfully.";
               Helpers.ProMessageBox.Show(msg, "Report Created");
            };

            _mediaViewModel.RequestAddToReport += (items) =>
            {
               _reportsViewModel.AddToReport(items);
               // Confirmation Popup instead of Navigation
               Helpers.ProMessageBox.Show("Media added to report successfully.", "Media Added");
            };

            // Player Integration
            // Player Integration
            _audioViewModel.RequestModifyReport += (path) =>
            {
                 var item = _reportsViewModel.GetReportItem(path);
                 if (item != null)
                 {
                     // Open Popup
                     // Use Dispatcher to ensure UI thread if needed (Event usually on UI thread but safe to be sure)
                     Application.Current.Dispatcher.Invoke(() => 
                     {
                         var win = new Views.QuickEditReportWindow(item);
                         win.Owner = Application.Current.MainWindow;
                         win.ShowDialog();
                     });
                 }
                 else
                 {
                     // Fallback: Navigate to Reports (User can see it's missing)
                     _reportsViewModel.NavigateToPath(path);
                     NavigateTo(PageType.Reports);
                 }
            };

            // _videoPlayerViewModel.RequestModifyReport Removed
            
            // Connect Player callback for Button Enability
            _videoPlayerViewModel.GetReportItemCallback = (path) => _reportsViewModel.GetReportItem(path);
            
            // ← NEW: Connect Player to Report for multi-rush clip logging
            _videoPlayerViewModel.AddClipToReportCallback = (clip) => _reportsViewModel.AddClipToCurrentReport(clip);

            // Real-time Feedback Loop
            // Real-time Feedback Loop
            _reportsViewModel.VideoReportItems.CollectionChanged += (s, e) =>
            {
                if (CurrentAppMode == AppMode.Video)
                {
                     var paths = _reportsViewModel.VideoReportItems.Select(x => x.OriginalMedia.FullName).ToList();
                     _mediaViewModel.UpdateReportContext(paths, _reportsViewModel.VideoReportItems.Any());
                     // Refresh Player Link in case current video was just added
                     _videoPlayerViewModel.RefreshReportLink();
                }
            };

            _reportsViewModel.AudioReportItems.CollectionChanged += (s, e) =>
            {
                if (CurrentAppMode == AppMode.Audio)
                {
                    var paths = _reportsViewModel.AudioReportItems.Select(x => x.OriginalMedia.FullName).ToList();
                    _mediaViewModel.UpdateReportContext(paths, _reportsViewModel.AudioReportItems.Any());
                }
            };
            
            // Generic Report Property Changes (Safety for IsReportActive)
            _reportsViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ReportsViewModel.IsReportActive))
                {
                     // If report status changes (e.g. cleared), refresh player button
                     Application.Current.Dispatcher.Invoke(() => _videoPlayerViewModel.RefreshReportLink());
                }
            };
        }

        private void NavigateTo(PageType page)
        {
            try 
            {
                CurrentPageType = page;
                UpdateCurrentView();
            }
            catch { /* Log */ }
        }

        /// <summary>
        /// Captures the current application state into a Session object
        /// </summary>
        private Veriflow.Core.Models.Session CaptureSessionState()
        {
            var session = new Veriflow.Core.Models.Session
            {
                CurrentMode = CurrentAppMode == AppMode.Audio ? "Audio" : "Video",
                CurrentPage = CurrentPageType.ToString(),
                MediaFiles = _mediaViewModel.GetLoadedFiles(),
                SecureCopySettings = new Veriflow.Core.Models.SecureCopySettings
                {
                    SourcePath = _secureCopyViewModel.SourcePath ?? string.Empty,
                    MainDestination = _secureCopyViewModel.Destination1Path ?? string.Empty,
                    SecondaryDestination = _secureCopyViewModel.Destination2Path ?? string.Empty
                },
                TranscodeQueue = _transcodeViewModel.GetQueuedFiles()
            };

            // Capture Audio Reports
            foreach (var item in _reportsViewModel.AudioReportItems)
            {
                session.AudioReportItems.Add(new Veriflow.Core.Models.ReportItemData
                {
                    FilePath = item.OriginalMedia.FullName,
                    FileName = item.Filename,
                    Scene = item.Scene,
                    Take = item.Take,
                    Notes = item.ItemNotes,
                    Timecode = item.StartTimeCode,
                    Duration = item.Duration
                });
            }

            // Capture Video Reports
            foreach (var item in _reportsViewModel.VideoReportItems)
            {
                var reportData = new Veriflow.Core.Models.ReportItemData
                {
                    FilePath = item.OriginalMedia.FullName,
                    FileName = item.Filename,
                    Scene = item.Scene,
                    Take = item.Take,
                    Notes = item.ItemNotes,
                    Timecode = item.StartTimeCode,
                    Duration = item.Duration,
                    Clips = new System.Collections.Generic.List<Veriflow.Core.Models.ClipData>()
                };

                // Capture clips if any
                if (item.Clips != null)
                {
                    foreach (var clip in item.Clips)
                    {
                        reportData.Clips.Add(new Veriflow.Core.Models.ClipData
                        {
                            InPoint = clip.InPoint,
                            OutPoint = clip.OutPoint,
                            Notes = clip.Notes
                        });
                    }
                }

                session.VideoReportItems.Add(reportData);
            }

            return session;
        }

        /// <summary>
        /// Restores application state from a Session object
        /// </summary>
        private void RestoreSessionState(Veriflow.Core.Models.Session session)
        {
            try
            {
                // Restore Mode
                var mode = session.CurrentMode == "Audio" ? AppMode.Audio : AppMode.Video;
                SetMode(mode);

                // Restore Media Files
                _mediaViewModel.LoadFiles(session.MediaFiles);

                // Restore SecureCopy Settings
                if (session.SecureCopySettings != null)
                {
                    _secureCopyViewModel.SourcePath = session.SecureCopySettings.SourcePath;
                    _secureCopyViewModel.Destination1Path = session.SecureCopySettings.MainDestination;
                    _secureCopyViewModel.Destination2Path = session.SecureCopySettings.SecondaryDestination;
                }

                // Restore Transcode Queue
                _transcodeViewModel.LoadFiles(session.TranscodeQueue);

                // Restore Reports
                _reportsViewModel.ClearAllReports();
                
                foreach (var reportData in session.AudioReportItems)
                {
                    _reportsViewModel.RestoreReportItem(reportData, isVideo: false);
                }

                foreach (var reportData in session.VideoReportItems)
                {
                    _reportsViewModel.RestoreReportItem(reportData, isVideo: true);
                }

                // Restore Page
                if (Enum.TryParse<PageType>(session.CurrentPage, out var pageType))
                {
                    NavigateTo(pageType);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error restoring session state:\n{ex.Message}",
                    "Restore Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void OpenSettings()
        {
            var window = new Views.SettingsWindow();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        private void OpenAbout()
        {
            var window = new Views.AboutWindow();
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }

        private void SetMode(AppMode mode)
        {
            CurrentAppMode = mode;
            UpdateTheme();
        }

        private void ToggleAudioVideoMode()
        {
            CurrentAppMode = CurrentAppMode == AppMode.Audio ? AppMode.Video : AppMode.Audio;
            UpdateTheme();
        }

        private void UpdateTheme()
        {
            try
            {
                // Dynamic Branding (Audio = Red, Video = Blue)
                string accentHex, hoverHex, pressedHex;

                if (CurrentAppMode == AppMode.Audio)
                {
                    accentHex = "#E64B3D";
                    hoverHex = "#FF6E60";
                    pressedHex = "#C03025";
                }
                else
                {
                    accentHex = "#1A4CB1";
                    hoverHex = "#3565C8";
                    pressedHex = "#123680";
                }

                if (Application.Current != null)
                {
                    Application.Current.Resources["Brush.Accent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentHex));
                    Application.Current.Resources["Brush.Accent.Hover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hoverHex));
                    Application.Current.Resources["Brush.Accent.Pressed"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pressedHex));

                    // Update Primary Accent Brush (used in some views)
                    Application.Current.Resources["PrimaryAccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentHex));

                    // Play button is always green in both modes
                    Application.Current.Resources["Brush.Transport.Play"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                }

                _mediaViewModel.SetAppMode(CurrentAppMode);
                _transcodeViewModel.SetAppMode(CurrentAppMode);
                
                // Push Report Context for the new mode immediately
                if (CurrentAppMode == AppMode.Video)
                {
                     var paths = _reportsViewModel.VideoReportItems.Select(x => x.OriginalMedia.FullName).ToList();
                     _mediaViewModel.UpdateReportContext(paths, _reportsViewModel.VideoReportItems.Any());
                }
                else
                {
                     var paths = _reportsViewModel.AudioReportItems.Select(x => x.OriginalMedia.FullName).ToList();
                     _mediaViewModel.UpdateReportContext(paths, _reportsViewModel.AudioReportItems.Any());
                }

                UpdateCurrentView();
            }
             catch { /* Log */ }
        }

        private void UpdateCurrentView()
        {
            switch (CurrentPageType)
            {
                case PageType.Media:
                    CurrentView = _mediaViewModel;
                    break;
                case PageType.Player:
                    // Smart switch based on Mode
                    if (CurrentAppMode == AppMode.Audio)
                        CurrentView = _audioViewModel;
                    else
                        CurrentView = _videoPlayerViewModel;
                    break;
                case PageType.SecureCopy:
                    CurrentView = _secureCopyViewModel;
                    break;
                case PageType.Transcode:
                    CurrentView = _transcodeViewModel;
                    break;
                case PageType.Sync:
                    CurrentView = _syncViewModel;
                    break;
                case PageType.Reports:
                    CurrentView = _reportsViewModel;
                    break;
            }
            
            NotifyNavigationProperties();
        }

        private void NotifyNavigationProperties()
        {
            OnPropertyChanged(nameof(IsMediaActive));
            OnPropertyChanged(nameof(IsPlayerActive));
            OnPropertyChanged(nameof(IsTranscodeActive));
            OnPropertyChanged(nameof(IsSyncActive));
            OnPropertyChanged(nameof(IsSecureCopyActive));
            OnPropertyChanged(nameof(IsReportsActive));
            OnPropertyChanged(nameof(IsAudioActive));
            OnPropertyChanged(nameof(IsVideoActive));
        }

        public bool IsMediaActive => CurrentPageType == PageType.Media;
        public bool IsPlayerActive => CurrentPageType == PageType.Player;
        public bool IsTranscodeActive => CurrentPageType == PageType.Transcode;
        public bool IsSecureCopyActive => CurrentPageType == PageType.SecureCopy;
        public bool IsSyncActive => CurrentPageType == PageType.Sync;
        public bool IsReportsActive => CurrentPageType == PageType.Reports;

        public bool IsAudioActive => CurrentAppMode == AppMode.Audio;
        public bool IsVideoActive => CurrentAppMode == AppMode.Video;

        public void Dispose()
        {
            // Dispose ViewModels with IDisposable (those with timers and media resources)
            _audioViewModel?.Dispose();
            _videoPlayerViewModel?.Dispose();
            _playerViewModel?.Dispose();
            
            // Note: Other ViewModels (MediaViewModel, SecureCopyViewModel, etc.) 
            // don't implement IDisposable as they don't have timers or unmanaged resources
            
            GC.SuppressFinalize(this);
        }

        // ========================================================================
        // EDIT MENU COMMANDS
        // ========================================================================

        private bool CanUndo() => _commandHistory.CanUndo;
        private bool CanRedo() => _commandHistory.CanRedo;

        private void Undo()
        {
            _commandHistory.Undo();
        }

        private void Redo()
        {
            _commandHistory.Redo();
        }

        private bool CanCutCopy()
        {
            // Enable for Reports, Media, and Transcode views
            return CurrentPageType == PageType.Reports ||
                   CurrentPageType == PageType.Media ||
                   CurrentPageType == PageType.Transcode;
        }

        private bool CanPaste()
        {
            // Check if clipboard has data
            return Clipboard.ContainsText() && CanCutCopy();
        }

        private void Cut()
        {
            Copy(); // Copy first
            Delete(); // Then delete
        }

        private void Delete()
        {
            switch (CurrentPageType)
            {
                case PageType.Reports:
                    if (_reportsViewModel.HasSelectedItems())
                    {
                        if (ShowDeleteConfirmation("Delete selected report item?"))
                        {
                            _reportsViewModel.DeleteSelectedItems();
                        }
                    }
                    break;
                case PageType.Media:
                    if (_mediaViewModel.HasSelectedFiles())
                    {
                        if (ShowDeleteConfirmation("Remove selected file from list?"))
                        {
                            _mediaViewModel.RemoveSelectedFiles();
                        }
                    }
                    break;
                case PageType.Transcode:
                    if (_transcodeViewModel.HasSelectedItems())
                    {
                        if (ShowDeleteConfirmation("Remove selected item from queue?"))
                        {
                            _transcodeViewModel.RemoveSelectedItems();
                        }
                    }
                    break;
            }
        }

        private bool ShowDeleteConfirmation(string message)
        {
            var settings = Services.SettingsService.Instance.GetSettings();
            if (!settings.ShowConfirmationDialogs)
                return true;

            var dialog = new Views.Shared.ProMessageBox(
                message,
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            dialog.Owner = System.Windows.Application.Current.MainWindow;
            return dialog.ShowDialog() == true;
        }

        private void Copy()
        {
            try
            {
                switch (CurrentPageType)
                {
                    case PageType.Reports:
                        CopyReportsToClipboard();
                        break;
                    case PageType.Media:
                        CopyMediaToClipboard();
                        break;
                    case PageType.Transcode:
                        CopyTranscodeToClipboard();
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error copying to clipboard:\n{ex.Message}",
                    "Copy Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Paste()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;

                var clipboardText = Clipboard.GetText();
                
                switch (CurrentPageType)
                {
                    case PageType.Reports:
                        // TODO: Paste report items from clipboard
                        MessageBox.Show("Paste to Reports not yet implemented.", "Paste", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    case PageType.Media:
                        // Parse file paths and load
                        var filePaths = clipboardText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        _mediaViewModel.LoadFiles(filePaths.ToList());
                        break;
                    case PageType.Transcode:
                        // Parse file paths and add to queue
                        var transcodeFiles = clipboardText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        _transcodeViewModel.LoadFiles(transcodeFiles.ToList());
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error pasting from clipboard:\n{ex.Message}",
                    "Paste Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CopyReportsToClipboard()
        {
            var reportItems = CurrentAppMode == AppMode.Audio
                ? _reportsViewModel.AudioReportItems
                : _reportsViewModel.VideoReportItems;

            if (reportItems.Count == 0)
            {
                MessageBox.Show("No reports to copy.", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Copy report file paths
            var filePaths = string.Join(Environment.NewLine, reportItems.Select(r => r.OriginalMedia.FullName));
            Clipboard.SetText(filePaths);
            MessageBox.Show($"Copied {reportItems.Count} report file path(s) to clipboard.", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CopyMediaToClipboard()
        {
            var loadedFiles = _mediaViewModel.GetLoadedFiles();
            if (loadedFiles.Count == 0)
            {
                MessageBox.Show("No media files to copy.", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var filePaths = string.Join(Environment.NewLine, loadedFiles);
            Clipboard.SetText(filePaths);
            MessageBox.Show($"Copied {loadedFiles.Count} file path(s) to clipboard.", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CopyTranscodeToClipboard()
        {
            var queuedFiles = _transcodeViewModel.GetQueuedFiles();
            if (queuedFiles.Count == 0)
            {
                MessageBox.Show("No transcode queue items to copy.", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var filePaths = string.Join(Environment.NewLine, queuedFiles);
            Clipboard.SetText(filePaths);
            MessageBox.Show($"Copied {queuedFiles.Count} queued file path(s) to clipboard.", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearCurrentPage()
        {
            var pageName = CurrentPageType.ToString();
            var result = MessageBox.Show(
                $"Are you sure you want to clear the {pageName} page?\n\nThis will reset the page to its default state.\n\nThis action cannot be undone.",
                "Clear Current Page",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                switch (CurrentPageType)
                {
                    case PageType.SecureCopy:
                        ClearSecureCopyPage();
                        break;
                    case PageType.Media:
                        ClearMediaPage();
                        break;
                    case PageType.Player:
                        ClearPlayerPage();
                        break;
                    case PageType.Sync:
                        ClearSyncPage();
                        break;
                    case PageType.Transcode:
                        ClearTranscodePage();
                        break;
                    case PageType.Reports:
                        ClearReportsPage();
                        break;
                }

                MessageBox.Show(
                    $"{pageName} page has been cleared.",
                    "Clear Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error clearing page:\n{ex.Message}",
                    "Clear Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ClearSecureCopyPage()
        {
            // Clear source and destination paths
            _secureCopyViewModel.SourcePath = string.Empty;
            _secureCopyViewModel.Destination1Path = string.Empty;
            _secureCopyViewModel.Destination2Path = string.Empty;
        }

        private void ClearMediaPage()
        {
            // Clear loaded media files and reset browser to default
            _mediaViewModel.LoadFiles(new List<string>());
            // Browser will reset to default state when files are cleared
        }

        private void ClearPlayerPage()
        {
            // Clear loaded media using commands
            if (CurrentAppMode == AppMode.Audio)
            {
                if (_audioViewModel.UnloadMediaCommand.CanExecute(null))
                {
                    _audioViewModel.UnloadMediaCommand.Execute(null);
                }
            }
            else
            {
                if (_videoPlayerViewModel.UnloadMediaCommand.CanExecute(null))
                {
                    _videoPlayerViewModel.UnloadMediaCommand.Execute(null);
                }
                // Clear logged clips list (Video profile only)
                // TODO: Add method to clear logged clips if needed
            }
        }

        private void ClearSyncPage()
        {
            // Clear sync datagrids and address field
            // TODO: Add clear methods to SyncViewModel when available
            MessageBox.Show(
                "Sync page clear not yet fully implemented.",
                "Clear Sync",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ClearTranscodePage()
        {
            // Clear media list but keep settings
            _transcodeViewModel.LoadFiles(new List<string>());
            // Settings panel remains unchanged
        }

        private void ClearReportsPage()
        {
            // Clear report list and EDL view
            _reportsViewModel.ClearAllReports();
            // Panel will reset when reports are cleared
        }

        // ========================================================================
        // HELP MENU COMMANDS
        // ========================================================================

        private void ViewHelp()
        {
            try
            {
                var helpWindow = new Views.HelpWindow();
                helpWindow.Owner = Application.Current.MainWindow;
                helpWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening help window:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ShowKeyboardShortcuts()
        {
            try
            {
                var shortcutsWindow = new Views.ShortcutsWindow
                {
                    Owner = Application.Current.MainWindow
                };
                shortcutsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening shortcuts window:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenLogFolder()
        {
            try
            {
                // Determine log folder location
                var logFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Veriflow",
                    "Logs");

                // Create folder if it doesn't exist
                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }

                // Open in Windows Explorer
                Process.Start(new ProcessStartInfo
                {
                    FileName = logFolder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening log folder:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
