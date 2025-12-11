using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

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
        private AppMode _currentAppMode = AppMode.Audio;

        [ObservableProperty]
        private PageType _currentPageType = PageType.Media;

        // ViewModels
        private readonly OffloadViewModel _offloadViewModel = new();
        private readonly PlayerViewModel _playerViewModel = new(); 
        private readonly AudioViewModel _audioViewModel = new();
        private readonly TranscodeViewModel _transcodeViewModel = new();
        private readonly MediaViewModel _mediaViewModel = new();
        private readonly SyncViewModel _syncViewModel = new();
        private readonly string _reportsView = "Reports View (Coming Soon)";

        public ICommand ShowPlayerCommand { get; }
        public ICommand ShowMediaCommand { get; }
        public ICommand ShowTranscodeCommand { get; }
        public ICommand ShowSyncCommand { get; }
        public ICommand ShowOffloadCommand { get; }
        public ICommand ShowReportsCommand { get; }
        public ICommand SwitchToAudioCommand { get; }
        public ICommand SwitchToVideoCommand { get; }

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

            // Default
            UpdateCurrentView();

            // Navigation Wiring
            _mediaViewModel.RequestOpenInPlayer += async (path) =>
            {
                try
                {
                    await _audioViewModel.LoadAudio(path);
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

             _playerViewModel.RequestTranscode += (files) =>
            {
                _transcodeViewModel.AddFiles(files);
                NavigateTo(PageType.Transcode);
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

        private void SetMode(AppMode mode)
        {
            try
            {
                CurrentAppMode = mode;
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
                        CurrentView = _playerViewModel;
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
                    CurrentView = _reportsView;
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
