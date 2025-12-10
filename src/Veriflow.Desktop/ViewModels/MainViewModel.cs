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
        private readonly MediaViewModel _mediaViewModel = new();
        private readonly string _reportsView = "Reports View (Coming Soon)";

        public ICommand ShowPlayerCommand { get; }
        public ICommand ShowMediaCommand { get; }
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
            ShowOffloadCommand = new RelayCommand(() => CurrentView = _offloadViewModel);
            ShowReportsCommand = new RelayCommand(() => CurrentView = _reportsView);

            // Default to Media view (as requested order suggests, or Player? User said Media is "before" Player)
            // Let's stick to Player as default for now or switch if requested. 
            // Actually, usually users want the "Home" page. Media might be the new home.
            // Let's keep Player as default for now to avoid            // Default to Media View as requested
            CurrentView = _mediaViewModel;
        }
    }
}
