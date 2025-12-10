using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Veriflow.Desktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "Veriflow Pro";

        [ObservableProperty]
        private object? _currentView;

        // ViewModels
        private readonly OffloadViewModel _offloadViewModel = new();
        private readonly PlayerViewModel _playerViewModel = new(); 
        private readonly TranscodeViewModel _transcodeViewModel = new();
        private readonly MediaViewModel _mediaViewModel = new();
        private readonly string _reportsView = "Reports View (Coming Soon)";

        public ICommand ShowPlayerCommand { get; }
        public ICommand ShowMediaCommand { get; }
        public ICommand ShowTranscodeCommand { get; }
        public ICommand ShowOffloadCommand { get; }
        public ICommand ShowReportsCommand { get; }

        public MainViewModel()
        {
            ShowPlayerCommand = new RelayCommand(() => CurrentView = _playerViewModel);
            ShowMediaCommand = new RelayCommand(() => 
            {
                Console.WriteLine(">>> DEBUG NAV: Attempting to set CurrentPage to MediaViewModel <<<");
                CurrentView = _mediaViewModel;
            });
            ShowTranscodeCommand = new RelayCommand(() => CurrentView = _transcodeViewModel);
            ShowOffloadCommand = new RelayCommand(() => CurrentView = _offloadViewModel);
            ShowReportsCommand = new RelayCommand(() => CurrentView = _reportsView);

            // Default to Media view (as requested order suggests, or Player? User said Media is "before" Player)
            // Let's stick to Player as default for now or switch if requested. 
            // Actually, usually users want the "Home" page. Media might be the new home.
            // Default to Media View as requested
            // Default to Media View as requested
            CurrentView = _mediaViewModel;

            // Navigation Wiring
            _mediaViewModel.RequestOpenInPlayer += async (path) =>
            {
                try
                {
                    await _playerViewModel.LoadAudio(path);
                    CurrentView = _playerViewModel;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error opening player: {ex.Message}");
                }
            };
        }

        partial void OnCurrentViewChanged(object? value)
        {
            OnPropertyChanged(nameof(IsMediaActive));
            OnPropertyChanged(nameof(IsPlayerActive));
            OnPropertyChanged(nameof(IsTranscodeActive));
            OnPropertyChanged(nameof(IsOffloadActive));
            OnPropertyChanged(nameof(IsReportsActive));
        }

        public bool IsMediaActive
        {
            get => CurrentView == _mediaViewModel;
            set { if (value) CurrentView = _mediaViewModel; }
        }

        public bool IsPlayerActive
        {
            get => CurrentView == _playerViewModel;
            set { if (value) CurrentView = _playerViewModel; }
        }

        public bool IsTranscodeActive
        {
            get => CurrentView == _transcodeViewModel;
            set { if (value) CurrentView = _transcodeViewModel; }
        }

        public bool IsOffloadActive
        {
            get => CurrentView == _offloadViewModel;
            set { if (value) CurrentView = _offloadViewModel; }
        }

        public bool IsReportsActive
        {
            get => object.Equals(CurrentView, _reportsView);
            set { if (value) CurrentView = _reportsView; }
        }
    }
}
