using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Veriflow.Desktop.Models;
using Veriflow.Desktop.Services;

namespace Veriflow.Desktop.ViewModels
{
    public partial class ReportsViewModel : ObservableObject
    {
        // --- DATA ---
        private ReportHeader _videoHeader = new() { ProductionCompany = "Veriflow Video" };
        private ReportHeader _audioHeader = new() { ProductionCompany = "SoundLog Pro Production" };

        [ObservableProperty] private ReportHeader _header;
        [ObservableProperty] private ObservableCollection<ReportItem> _reportItems = new();
        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(ReportTitle))]
        private ReportType _currentReportType = ReportType.Video;

        partial void OnCurrentReportTypeChanged(ReportType value)
        {
            if (value == ReportType.Audio)
            {
                Header = _audioHeader;
            }
            else
            {
                Header = _videoHeader;
            }
        }

        public string ReportTitle => CurrentReportType == ReportType.Audio ? "SOUND REPORT" : "CAMERA REPORT";

        // --- STATE ---
        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(HasReport))]
        private bool _isReportActive;

        public bool HasReport => IsReportActive && ReportItems.Count > 0;



        [ObservableProperty] private bool _isVideoCalendarOpen;
        [ObservableProperty] private bool _isAudioCalendarOpen;

        // --- NAVIGATION REQUEST ---
        // Simple event or messenger could be used. For simplicity, we might assume MainViewModel 
        // observes this or we inject a service. 
        // Let's keep it simple: MainViewModel has access to this VM.

        public ReportsViewModel()
        {
            _printingService = new ReportPrintingService();
            _header = _videoHeader; // Initialize default
            SubscribeToHeader();
            ReportItems.CollectionChanged += (s, e) => ClearListCommand.NotifyCanExecuteChanged();
            // Default title logic handles initialization
        }

        public void SetAppMode(AppMode mode)
        {
            CurrentReportType = mode == AppMode.Audio ? ReportType.Audio : ReportType.Video;
        }

        private readonly IReportPrintingService _printingService;

        // --- ACTIONS ---

        public void CreateReport(IEnumerable<MediaItemViewModel> items, ReportType type)
        {
            CurrentReportType = type;
            
            // Re-initialize specific header
            if (type == ReportType.Audio) 
            {
                 _audioHeader = new ReportHeader() { ProductionCompany = "SoundLog Pro Production" };
                 Header = _audioHeader;
            }
            else 
            {
                 _videoHeader = new ReportHeader() { ProductionCompany = "Veriflow Video" };
                 Header = _videoHeader;
            }

            ReportItems.Clear();
            foreach (var item in items)
            {
                ReportItems.Add(new ReportItem(item));
            }

            IsReportActive = true;
        }

        public void AddToReport(IEnumerable<MediaItemViewModel> items)
        {
            if (!IsReportActive) return;

            foreach (var item in items)
            {
                ReportItems.Add(new ReportItem(item));
            }
        }

        [RelayCommand]
        private void AddFiles()
        {
            // TODO: Implement file picker
        }

        private bool CanClearList() => ReportItems.Count > 0;

        [RelayCommand(CanExecute = nameof(CanClearList))]
        private void ClearList()
        {
            ReportItems.Clear();
        }






        public void NavigateToItem(MediaItemViewModel item)
        {
            var reportItem = ReportItems.FirstOrDefault(r => r.OriginalMedia == item);
            if (reportItem != null)
            {
                SelectedReportItem = reportItem;
            }
        }

        public ReportItem? GetReportItem(string path)
        {
            return ReportItems.FirstOrDefault(r => r.OriginalMedia.FullName.Equals(path, System.StringComparison.OrdinalIgnoreCase));
        }

        public void NavigateToPath(string path)
        {
             var reportItem = GetReportItem(path);
             if (reportItem != null)
             {
                 SelectedReportItem = reportItem;
             }
        }

        public bool HasMedia => ReportItems.Count > 0;
        
        public bool HasInfos
        {
            get
            {
                if (Header == null) return false;
                // Check if any significant field is modified from default
                bool hasData = !string.IsNullOrEmpty(Header.ReportDate) ||
                               !string.IsNullOrEmpty(Header.ProjectName) || 
                               !string.IsNullOrEmpty(Header.OperatorName) || 
                               !string.IsNullOrEmpty(Header.Director) ||
                               !string.IsNullOrEmpty(Header.Dop) ||
                               !string.IsNullOrEmpty(Header.SoundMixer) ||
                               !string.IsNullOrEmpty(Header.Episode) ||
                               !string.IsNullOrEmpty(Header.Scene) ||
                               !string.IsNullOrEmpty(Header.Take) ||
                               !string.IsNullOrEmpty(Header.GlobalNotes);
                
                // Account for default Production Company
                // If it differs from default, it's info. 
                // However, user might consider just the structure itself.
                // Let's stick to user inputs. 
                // If user typed anything, it returns true.
                return hasData;
            }
        }
        public bool HasAnyData => HasMedia || HasInfos;


        [RelayCommand(CanExecute = nameof(HasMedia))]
        private void ClearMedia()
        {
            ReportItems.Clear();
            OnPropertyChanged(nameof(HasReport));
            OnPropertyChanged(nameof(HasMedia));
            OnPropertyChanged(nameof(HasAnyData));
            ClearMediaCommand.NotifyCanExecuteChanged();
            ClearAllCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(HasInfos))]
        private void ClearInfos()
        {
            Header = new ReportHeader();
            // Retain default values logic
             if (CurrentReportType == ReportType.Audio) 
             {
                 Header.ProductionCompany = "SoundLog Pro Production";
                 _audioHeader = Header;
             }
            else 
            {
                Header.ProductionCompany = "Veriflow Video";
                _videoHeader = Header;
            }
            
            SubscribeToHeader(); 
            OnPropertyChanged(nameof(HasInfos));
            OnPropertyChanged(nameof(HasAnyData));
             ClearInfosCommand.NotifyCanExecuteChanged();
            ClearAllCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(HasAnyData))]
        private void ClearAll()
        {
            ClearMedia();
            ClearInfos(); // This handles notifications
        }
        
        // Hook into Header changes
        partial void OnHeaderChanged(ReportHeader value)
        {
            SubscribeToHeader();
            OnPropertyChanged(nameof(HasInfos));
            OnPropertyChanged(nameof(HasAnyData));
            ClearInfosCommand.NotifyCanExecuteChanged();
            ClearAllCommand.NotifyCanExecuteChanged();
        }

        private void SubscribeToHeader()
        {
            if (Header != null)
            {
                Header.PropertyChanged -= Header_PropertyChanged;
                Header.PropertyChanged += Header_PropertyChanged;
            }
        }

        private void Header_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
             if (e.PropertyName == nameof(ReportHeader.CalendarDate))
             {
                 if (CurrentReportType == ReportType.Audio) IsAudioCalendarOpen = false;
                 else IsVideoCalendarOpen = false;
             }
 
             OnPropertyChanged(nameof(HasInfos));
             OnPropertyChanged(nameof(HasAnyData));
             ClearInfosCommand.NotifyCanExecuteChanged();
             ClearAllCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty] private ReportItem? _selectedReportItem;


        // --- COMMANDS ---
        
        [RelayCommand(CanExecute = nameof(HasReport))]
        private void Print()
        {
            if (!IsReportActive) return;
            _printingService.PrintReport(Header, ReportItems, CurrentReportType);
        }

        [RelayCommand(CanExecute = nameof(HasReport))]
        private void ExportPdf()
        {
             if (!IsReportActive) return;
             // Re-use Print logic
             _printingService.PrintReport(Header, ReportItems, CurrentReportType);
        }
    }
}
