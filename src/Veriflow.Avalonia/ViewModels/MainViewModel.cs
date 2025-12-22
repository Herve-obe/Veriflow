using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input; // For ICommand
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Veriflow.Avalonia.Models;

namespace Veriflow.Avalonia.ViewModels
{
    public enum AppMode { Audio, Video }
    public enum PageType { Media, Player, Sync, Offload, Transcode, Reports }

    public partial class MainViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private string _title = "Veriflow";

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
        public OffloadViewModel OffloadViewModel { get; }
        public PlayerViewModel PlayerViewModel { get; }
        public AudioPlayerViewModel AudioViewModel { get; }
        public VideoPlayerViewModel VideoPlayerViewModel { get; }
        public TranscodeViewModel TranscodeViewModel { get; }
        public MediaViewModel MediaViewModel { get; }
        public SyncViewModel SyncViewModel { get; }
        public ReportsViewModel ReportsViewModel { get; }
        public SessionViewModel SessionViewModel { get; }

        // Private backing fields
        private readonly OffloadViewModel _offloadViewModel;
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
        public ICommand ShowOffloadCommand { get; }
        public ICommand ShowReportsCommand { get; }
        
        // Navigation command aliases for MainWindow bindings
        public ICommand NavigateToOffloadCommand => ShowOffloadCommand;
        public ICommand NavigateToMediaCommand => ShowMediaCommand;
        public ICommand NavigateToPlayerCommand => ShowPlayerCommand;
        public ICommand NavigateToSyncCommand => ShowSyncCommand;
        public ICommand NavigateToTranscodeCommand => ShowTranscodeCommand;
        public ICommand NavigateToReportsCommand => ShowReportsCommand;
        
        // Page check properties for RadioButton IsChecked bindings
        public bool IsOffloadPage => CurrentPageType == PageType.Offload;
        public bool IsMediaPage => CurrentPageType == PageType.Media;
        public bool IsPlayerPage => CurrentPageType == PageType.Player;
        public bool IsSyncPage => CurrentPageType == PageType.Sync;
        public bool IsTranscodePage => CurrentPageType == PageType.Transcode;
        public bool IsReportsPage => CurrentPageType == PageType.Reports;
        
        // Mode check properties for ToggleButton IsChecked bindings
        public bool IsVideoMode => CurrentAppMode == AppMode.Video;
        public bool IsAudioMode => CurrentAppMode == AppMode.Audio;
        
        // CurrentViewModel for ContentControl binding
        public object? CurrentViewModel => CurrentView;
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
        
        // Recent Files
        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<RecentFileEntry> _recentFiles = new();

        public MainViewModel()
        {
            // Initialize ViewModels
            _offloadViewModel = new(new Services.OffloadService(), new Services.MhlService());
            // _playerViewModel = new(); // PlayerViewModel might not be used directly if split into Audio/Video? 
            // Assuming PlayerViewModel base or shared logic exists, but user code had it.
            // Based on previous file content, it was instantiated.
            _playerViewModel = new PlayerViewModel(); 
            
            _audioViewModel = new();
            _videoPlayerViewModel = new();
            _transcodeViewModel = new();
            _mediaViewModel = new();
            _syncViewModel = new();
            _reportsViewModel = new();

            // Expose as public properties
            OffloadViewModel = _offloadViewModel;
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
            ShowOffloadCommand = new RelayCommand(() => NavigateTo(PageType.Offload));
            ShowReportsCommand = new RelayCommand(() => NavigateTo(PageType.Reports));

            SwitchToAudioCommand = new RelayCommand(() => SetMode(AppMode.Audio));
            SwitchToVideoCommand = new RelayCommand(() => SetMode(AppMode.Video));
            ToggleAudioVideoModeCommand = new RelayCommand(ToggleAudioVideoMode);
            OpenAboutCommand = new RelayCommand(OpenAbout);
            ExitCommand = new RelayCommand(() => 
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            });

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
            PasteCommand = new AsyncRelayCommand(Paste, CanPaste);
            ClearCurrentPageCommand = new RelayCommand(ClearCurrentPage);
            
            // Help Commands
            ViewHelpCommand = new RelayCommand(ViewHelp);
            ShowKeyboardShortcutsCommand = new RelayCommand(ShowKeyboardShortcuts);
            OpenLogFolderCommand = new RelayCommand(OpenLogFolder);

            // Default
            SetMode(AppMode.Video);
            NavigateTo(PageType.Offload); // Set start page to OFFLOAD

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
                    System.Diagnostics.Debug.WriteLine($"Error opening player: {ex.Message}");
                }
            };

            _mediaViewModel.RequestOffloadSource += (path) =>
            {
                _offloadViewModel.SourcePath = path;
                NavigateTo(PageType.Offload);
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
               // Helpers.ProMessageBox.Show(msg, "Report Created"); // Stub
               System.Diagnostics.Debug.WriteLine(msg);
            };

            _mediaViewModel.RequestAddToReport += (items) =>
            {
               _reportsViewModel.AddToReport(items);
               // Confirmation Popup instead of Navigation
               // Helpers.ProMessageBox.Show("Media added to report successfully.", "Media Added"); // Stub
               System.Diagnostics.Debug.WriteLine("Media added to report successfully.");
            };

            // Player Integration
            // Player Integration
            _audioViewModel.RequestModifyReport += (path) =>
            {
                 var item = _reportsViewModel.GetReportItem(path);
                 if (item != null)
                 {
                     // Open Popup
                     Dispatcher.UIThread.Invoke(() => 
                     {
                         // var win = new Views.QuickEditReportWindow(item);
                         // win.Owner = Application.Current.MainWindow;
                         // win.ShowDialog();
                         System.Diagnostics.Debug.WriteLine("QuickEditReportWindow Stub");
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
                     Dispatcher.UIThread.Invoke(() => _videoPlayerViewModel.RefreshReportLink());
                }
            };

            // CommandHistory event subscription
            _commandHistory.StateChanged += (s, e) =>
            {
                // Refresh Undo/Redo command states
                (UndoCommand as RelayCommand)?.NotifyCanExecuteChanged();
                (RedoCommand as RelayCommand)?.NotifyCanExecuteChanged();
            };

            // Expose CommandHistory to ReportsViewModel
            _reportsViewModel.ExecuteCommandCallback = (cmd) => _commandHistory.ExecuteCommand(cmd);
            
            // Load recent files
            LoadRecentFiles();
            
            // Subscribe to recent files changes
            Services.RecentFilesService.Instance.RecentFilesChanged += OnRecentFilesChanged;
            
            // Subscribe to auto-save events
            _sessionViewModel.OnAutoSaveCompleted += OnAutoSaveCompleted;
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

                OffloadSettings = new Veriflow.Core.Models.OffloadSettings
                {
                    SourcePath = _offloadViewModel.SourcePath ?? string.Empty,
                    MainDestination = _offloadViewModel.Destination1Path ?? string.Empty,
                    SecondaryDestination = _offloadViewModel.Destination2Path ?? string.Empty
                },
                TranscodeQueue = _transcodeViewModel.GetQueuedFiles(),
                ReportSettings = _reportsViewModel.ReportSettings.Clone()
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

                // Restore Offload Settings
                if (session.OffloadSettings != null)
                {
                    _offloadViewModel.SourcePath = session.OffloadSettings.SourcePath;
                    _offloadViewModel.Destination1Path = session.OffloadSettings.MainDestination;
                    _offloadViewModel.Destination2Path = session.OffloadSettings.SecondaryDestination;
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
                // Restore Report Settings
                if (session.ReportSettings != null)
                {
                    _reportsViewModel.ReportSettings = session.ReportSettings;
                }

                // Restore Page
                if (Enum.TryParse<PageType>(session.CurrentPage, out var pageType))
                {
                    NavigateTo(pageType);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring session state: {ex.Message}");
            }
        }

        private void OpenSettings()
        {
            // var window = new Views.SettingsWindow();
            // window.Owner = Application.Current.MainWindow;
            // window.ShowDialog();
            System.Diagnostics.Debug.WriteLine("SettingsWindow Stub");
        }

        private void OpenAbout()
        {
            // var window = new Views.AboutWindow();
            // window.Owner = Application.Current.MainWindow;
            // window.ShowDialog();
            System.Diagnostics.Debug.WriteLine("AboutWindow Stub");
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
                    var accentBrush = SolidColorBrush.Parse(accentHex);
                    var hoverBrush = SolidColorBrush.Parse(hoverHex);
                    var pressedBrush = SolidColorBrush.Parse(pressedHex);
                    
                    Application.Current.Resources["Brush.Accent"] = accentBrush;
                    Application.Current.Resources["Brush.Accent.Hover"] = hoverBrush;
                    Application.Current.Resources["Brush.Accent.Pressed"] = pressedBrush;

                    // Update Primary Accent Brush (used in some views)
                    Application.Current.Resources["PrimaryAccentBrush"] = accentBrush;

                    // Play button is always green in both modes
                    Application.Current.Resources["Brush.Transport.Play"] = SolidColorBrush.Parse("#4CAF50");
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
                case PageType.Offload:
                    CurrentView = _offloadViewModel;
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
            OnPropertyChanged(nameof(IsOffloadActive));
            OnPropertyChanged(nameof(IsReportsActive));
            OnPropertyChanged(nameof(IsAudioActive));
            OnPropertyChanged(nameof(IsVideoActive));
            
            // Also notify page check properties for RadioButton bindings
            OnPropertyChanged(nameof(IsOffloadPage));
            OnPropertyChanged(nameof(IsMediaPage));
            OnPropertyChanged(nameof(IsPlayerPage));
            OnPropertyChanged(nameof(IsSyncPage));
            OnPropertyChanged(nameof(IsTranscodePage));
            OnPropertyChanged(nameof(IsReportsPage));
            OnPropertyChanged(nameof(CurrentViewModel));
        }

        public bool IsMediaActive => CurrentPageType == PageType.Media;
        public bool IsPlayerActive => CurrentPageType == PageType.Player;
        public bool IsTranscodeActive => CurrentPageType == PageType.Transcode;
        public bool IsOffloadActive => CurrentPageType == PageType.Offload;
        public bool IsSyncActive => CurrentPageType == PageType.Sync;
        public bool IsReportsActive => CurrentPageType == PageType.Reports;

        public bool IsAudioActive => CurrentAppMode == AppMode.Audio;
        public bool IsVideoActive => CurrentAppMode == AppMode.Video;

        // ========================================================================
        // RECENT FILES
        // ========================================================================

        private void LoadRecentFiles()
        {
            var files = Services.RecentFilesService.Instance.GetRecentFiles();
            RecentFiles.Clear();
            foreach (var file in files)
            {
                RecentFiles.Add(file);
            }
        }

        private void OnRecentFilesChanged(object? sender, EventArgs e)
        {
            // Update on UI thread
            Dispatcher.UIThread.Invoke(() =>
            {
                LoadRecentFiles();
            });
        }

        [RelayCommand]
        private void OpenRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                // File no longer exists, it will be filtered out automatically on next load
                System.Diagnostics.Debug.WriteLine($"File not found: {filePath}");
                
                // Refresh the list to remove non-existent files
                LoadRecentFiles();
                return;
            }

            // Determine file type and open in appropriate player
            var extension = Path.GetExtension(filePath).ToLower();

            // Audio extensions
            var audioExtensions = new[] { ".wav", ".mp3", ".m4a", ".aac", ".aiff", ".aif", ".flac", ".ogg", ".opus", ".ac3" };

            if (audioExtensions.Contains(extension))
            {
                // Switch to audio mode and open in audio player
                SwitchToAudio();
                ShowPlayer();

                if (CurrentView is AudioPlayerViewModel audioPlayer)
                {
                    _ = audioPlayer.LoadAudio(filePath);
                }
            }
            else
            {
                // Assume video, switch to video mode and open in video player
                SwitchToVideo();
                ShowPlayer();

                if (CurrentView is VideoPlayerViewModel videoPlayer)
                {
                    _ = videoPlayer.LoadVideo(filePath);
                }
            }
        }

        private void SwitchToAudio()
        {
            if (CurrentAppMode != AppMode.Audio)
            {
                SwitchToAudioCommand.Execute(null);
            }
        }

        private void SwitchToVideo()
        {
            if (CurrentAppMode != AppMode.Video)
            {
                SwitchToVideoCommand.Execute(null);
            }
        }

        private void ShowPlayer()
        {
            NavigateTo(PageType.Player);
        }

        // ========================================================================
        // AUTO-SAVE NOTIFICATION
        // ========================================================================

        private void OnAutoSaveCompleted(object? sender, EventArgs e)
        {
            // Show discreet notification
            Dispatcher.UIThread.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"Session auto-saved at {DateTime.Now:HH:mm:ss}");
            });
        }

        public void Dispose()
        {
            // Unsubscribe from recent files
            Services.RecentFilesService.Instance.RecentFilesChanged -= OnRecentFilesChanged;
            
            // Unsubscribe from auto-save
            _sessionViewModel.OnAutoSaveCompleted -= OnAutoSaveCompleted;
            
            // Dispose ViewModels with IDisposable (those with timers and media resources)
            _audioViewModel?.Dispose();
            _videoPlayerViewModel?.Dispose();
            _playerViewModel?.Dispose();
            (_sessionViewModel as IDisposable)?.Dispose();
            
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
            // Check if clipboard has data - Async in Avalonia usually, stub for now as we don't have TopLevel readily available without hack
            // We can return true and checking in Paste
            return CanCutCopy(); 
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
                        _transcodeViewModel.RemoveSelectedItems();
                    }
                    break;
            }
        }

        private void Copy()
        {
             // Stubbing Clipboard for now
             System.Diagnostics.Debug.WriteLine("Copy Command Executed - Clipboard Stub");
        }

        private async Task Paste()
        {
             // Stubbing Clipboard
             System.Diagnostics.Debug.WriteLine("Paste Command Executed - Clipboard Stub");
             await Task.CompletedTask;
        }

        private void ClearCurrentPage()
        {
            if (ShowDeleteConfirmation("Are you sure you want to clear all items from this page?"))
            {
                switch (CurrentPageType)
                {
                    case PageType.Media:
                        _mediaViewModel.ClearAllFiles();
                        break;
                    case PageType.Reports:
                        _reportsViewModel.ClearAll();
                        break;
                    case PageType.Transcode:
                        _transcodeViewModel.ClearListCommand.Execute(null);
                        break;
                    case PageType.Sync:
                        _syncViewModel.ClearAllCommand.Execute(null);
                        break;
                    case PageType.Offload:
                         _offloadViewModel.SourcePath = "";
                         _offloadViewModel.Destination1Path = "";
                         _offloadViewModel.Destination2Path = "";
                        break;
                }
            }
        }

        private void ViewHelp()
        {
             // Open file PDF or web link
             // Process.Start(new ProcessStartInfo { FileName = "...", UseShellExecute = true });
             System.Diagnostics.Debug.WriteLine("ViewHelp Stub");
        }

        private void ShowKeyboardShortcuts()
        {
             // new Views.ShortcutsWindow().ShowDialog();
             System.Diagnostics.Debug.WriteLine("ShortcutsWindow Stub");
        }

        private void OpenLogFolder()
        {
             // Process.Start("explorer.exe", "...");
             System.Diagnostics.Debug.WriteLine("OpenLogFolder Stub");
        }

        private bool ShowDeleteConfirmation(string message)
        {
            // Stub dialog integration. Return true for now or implement properly.
            // In a real app, use a DialogService.
            return true; 
        }
    }
}
