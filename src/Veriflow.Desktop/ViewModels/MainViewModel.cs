using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Veriflow.Desktop.Models; // Ensure this is present

namespace Veriflow.Desktop.ViewModels
{


    public enum AppMode { Audio, Video }
    public enum PageType { Media, Player, Sync, Offload, Transcode, Reports }

    public partial class MainViewModel : ObservableObject
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

        [ObservableProperty]
        private PageType _currentPageType = PageType.Media;

        // ViewModels
        private readonly OffloadViewModel _offloadViewModel = new();
        private readonly PlayerViewModel _playerViewModel = new(); 
        private readonly AudioViewModel _audioViewModel = new();
        private readonly VideoPlayerViewModel _videoPlayerViewModel = new();
        private readonly TranscodeViewModel _transcodeViewModel = new();
        private readonly MediaViewModel _mediaViewModel = new();
        private readonly SyncViewModel _syncViewModel = new();
        private readonly ReportsViewModel _reportsViewModel = new();

        public ICommand ShowPlayerCommand { get; }
        public ICommand ShowMediaCommand { get; }
        public ICommand ShowTranscodeCommand { get; }
        public ICommand ShowSyncCommand { get; }
        public ICommand ShowOffloadCommand { get; }
        public ICommand ShowReportsCommand { get; }
        public ICommand SwitchToAudioCommand { get; }
        public ICommand SwitchToVideoCommand { get; }
        public ICommand OpenAboutCommand { get; }
        public ICommand ExitCommand { get; }

        public MainViewModel()
        {
            // Navigation Commands
            ShowPlayerCommand = new RelayCommand(() => NavigateTo(PageType.Player));
            ShowMediaCommand = new RelayCommand(() => NavigateTo(PageType.Media));
            ShowTranscodeCommand = new RelayCommand(() => NavigateTo(PageType.Transcode));
            ShowSyncCommand = new RelayCommand(() => NavigateTo(PageType.Sync));
            ShowOffloadCommand = new RelayCommand(() => NavigateTo(PageType.Offload));
            ShowReportsCommand = new RelayCommand(() => NavigateTo(PageType.Reports));

            SwitchToAudioCommand = new RelayCommand(() => SetMode(AppMode.Audio));
            SwitchToVideoCommand = new RelayCommand(() => SetMode(AppMode.Video));
            OpenAboutCommand = new RelayCommand(OpenAbout);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

            // Default
            SetMode(AppMode.Video);
            NavigateTo(PageType.Offload); // Set start page to SECURE COPY (Offload)

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

        private void OpenAbout()
        {
            var window = new Views.AboutWindow();
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }

        private void SetMode(AppMode mode)
        {
            try
            {
                CurrentAppMode = mode;

                // Dynamic Branding (Audio = Red, Video = Blue)
                string accentHex, hoverHex, pressedHex;

                if (mode == AppMode.Audio)
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
                }

                _mediaViewModel.SetAppMode(mode);
                _transcodeViewModel.SetAppMode(mode);
                
                // Push Report Context for the new mode immediately
                if (mode == AppMode.Video)
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
        }

        public bool IsMediaActive => CurrentPageType == PageType.Media;
        public bool IsPlayerActive => CurrentPageType == PageType.Player;
        public bool IsTranscodeActive => CurrentPageType == PageType.Transcode;
        public bool IsOffloadActive => CurrentPageType == PageType.Offload;
        public bool IsSyncActive => CurrentPageType == PageType.Sync;
        public bool IsReportsActive => CurrentPageType == PageType.Reports;

        public bool IsAudioActive => CurrentAppMode == AppMode.Audio;
        public bool IsVideoActive => CurrentAppMode == AppMode.Video;
    }
}
