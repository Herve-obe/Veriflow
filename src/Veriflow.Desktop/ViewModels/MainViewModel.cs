using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Veriflow.Desktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "Veriflow";

        [ObservableProperty]
        private object? _currentView;

        // ViewModels
        private readonly OffloadViewModel _offloadViewModel = new();
        private readonly string _playerView = "Player / Metadata View (Coming Soon)"; 
        private readonly string _reportsView = "Reports View (Coming Soon)";

        public ICommand NavigatePlayerCommand { get; }
        public ICommand NavigateOffloadCommand { get; }
        public ICommand NavigateReportsCommand { get; }

        public MainViewModel()
        {
            NavigatePlayerCommand = new RelayCommand(() => CurrentView = _playerView);
            NavigateOffloadCommand = new RelayCommand(() => CurrentView = _offloadViewModel);
            NavigateReportsCommand = new RelayCommand(() => CurrentView = _reportsView);

            // Default to Player view
            CurrentView = _playerView;
        }
    }
}
