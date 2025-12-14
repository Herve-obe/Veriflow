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
        
        // Strict Separation
        [ObservableProperty] private ObservableCollection<ReportItem> _videoReportItems = new();
        [ObservableProperty] private ObservableCollection<ReportItem> _audioReportItems = new();

        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(ReportTitle))]
        [NotifyPropertyChangedFor(nameof(HasMedia))]
        [NotifyPropertyChangedFor(nameof(HasAnyData))]
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
            ClearListCommand.NotifyCanExecuteChanged();
            ClearMediaCommand.NotifyCanExecuteChanged();
        }

        public string ReportTitle => CurrentReportType == ReportType.Audio ? "SOUND REPORT" : "CAMERA REPORT";

        public ObservableCollection<ReportItem> CurrentReportItems => CurrentReportType == ReportType.Audio ? AudioReportItems : VideoReportItems;

        // --- STATE ---
        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(HasReport))]
        private bool _isReportActive;

        public bool HasReport => IsReportActive && HasMedia;

        [ObservableProperty] private bool _isVideoCalendarOpen;
        [ObservableProperty] private bool _isAudioCalendarOpen;

        private readonly IReportPrintingService _printingService;
        private readonly PdfReportService _pdfService;

        public ReportsViewModel()
        {
            _printingService = new ReportPrintingService();
            _pdfService = new PdfReportService();
            _header = _videoHeader; // Initialize default
            SubscribeToHeader();
            
            // Subscriptions to triggers
            VideoReportItems.CollectionChanged += (s, e) => NotifyCollectionChanges();
            AudioReportItems.CollectionChanged += (s, e) => NotifyCollectionChanges();
        }

        private void NotifyCollectionChanges()
        {
            OnPropertyChanged(nameof(HasMedia));
            OnPropertyChanged(nameof(HasReport));
            OnPropertyChanged(nameof(HasAnyData));
             // Explicitly notify commands
            ClearListCommand.NotifyCanExecuteChanged();
            ClearMediaCommand.NotifyCanExecuteChanged();
            PrintCommand.NotifyCanExecuteChanged();
            ExportPdfCommand.NotifyCanExecuteChanged();
        }

        public void SetAppMode(AppMode mode)
        {
            CurrentReportType = mode == AppMode.Audio ? ReportType.Audio : ReportType.Video;
        }

        // --- ACTIONS ---

        public void CreateReport(IEnumerable<MediaItemViewModel> items, ReportType type)
        {
            CurrentReportType = type;
            
            // Re-initialize specific header if needed
            if (type == ReportType.Audio) 
            {
                 if (_audioHeader == null) _audioHeader = new ReportHeader() { ProductionCompany = "SoundLog Pro Production" };
                 Header = _audioHeader;
                 AudioReportItems.Clear();
                 foreach (var item in items) AudioReportItems.Add(new ReportItem(item));
            }
            else 
            {
                 if (_videoHeader == null) _videoHeader = new ReportHeader() { ProductionCompany = "Veriflow Video" };
                 Header = _videoHeader;
                 VideoReportItems.Clear();
                 foreach (var item in items) VideoReportItems.Add(new ReportItem(item));
            }

            IsReportActive = true;
        }

        public void AddToReport(IEnumerable<MediaItemViewModel> items)
        {
            if (!IsReportActive) return;

            var targetCollection = CurrentReportType == ReportType.Audio ? AudioReportItems : VideoReportItems;
            foreach (var item in items)
            {
                targetCollection.Add(new ReportItem(item));
            }
        }

        [RelayCommand]
        private void AddFiles()
        {
            // TODO: Implement file picker
        }

        [RelayCommand(CanExecute = nameof(HasMedia))]
        private void ClearList()
        {
             if (CurrentReportType == ReportType.Audio)
                AudioReportItems.Clear();
            else
                VideoReportItems.Clear();
        }

        [RelayCommand(CanExecute = nameof(CanRemoveFile))]
        private void RemoveFile(ReportItem item)
        {
            if (item != null)
            {
                if (CurrentReportType == ReportType.Audio)
                    AudioReportItems.Remove(item);
                else
                    VideoReportItems.Remove(item);
            }
        }

        private bool CanRemoveFile(ReportItem item) => item != null;

        public void NavigateToItem(MediaItemViewModel item)
        {
             var reportItem = CurrentReportItems.FirstOrDefault(r => r.OriginalMedia == item);
             if (reportItem != null)
             {
                 SelectedReportItem = reportItem;
             }
        }

        public ReportItem? GetReportItem(string path)
        {
            // Condition 1: Report Generated (Active)
            if (!IsReportActive) return null;

            // Condition 2: List not empty (Optimization)
            if (!HasMedia) return null;

            // Condition 3: File match (Robust Path Comparison)
            try
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                return CurrentReportItems.FirstOrDefault(r => 
                    string.Equals(System.IO.Path.GetFullPath(r.OriginalMedia.FullName), fullPath, System.StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                // Fallback to simple comparison if path is invalid
                return CurrentReportItems.FirstOrDefault(r => r.OriginalMedia.FullName.Equals(path, System.StringComparison.OrdinalIgnoreCase));
            }
        }

        public void NavigateToPath(string path)
        {
             var reportItem = GetReportItem(path);
             if (reportItem != null)
             {
                 SelectedReportItem = reportItem;
             }
        }

        public bool HasMedia
        {
            get
            {
                return CurrentReportType == ReportType.Audio ? AudioReportItems.Count > 0 : VideoReportItems.Count > 0;
            }
        }
        
        public bool HasInfos
        {
            get
            {
                if (Header == null) return false;
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
                return hasData;
            }
        }
        public bool HasAnyData => HasMedia || HasInfos;


        [RelayCommand(CanExecute = nameof(HasMedia))]
        private void ClearMedia()
        {
            ClearList(); // Logic reused
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
            ClearInfos(); 
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
            _printingService.PrintReport(Header, CurrentReportItems, CurrentReportType);
        }

        [RelayCommand(CanExecute = nameof(HasReport))]
        private void ExportPdf()
        {
             if (!IsReportActive) return;

             var dlg = new Microsoft.Win32.SaveFileDialog
             {
                 FileName = CurrentReportType == ReportType.Video ? "camera_report" : "sound_report",
                 DefaultExt = ".pdf",
                 Filter = "PDF Documents (.pdf)|*.pdf"
             };

             if (dlg.ShowDialog() == true)
             {
                 try
                 {
                     bool isVideo = CurrentReportType == ReportType.Video;
                     _pdfService.GeneratePdf(dlg.FileName, Header, CurrentReportItems, isVideo);
                 }
                 catch (System.IO.IOException)
                 {
                     MessageBox.Show($"The file '{dlg.FileName}' is currently open in another application.\n\nPlease close the file and try again.", "File Access Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 }
                 catch (System.Exception ex)
                 {
                     MessageBox.Show($"An error occurred while saving the report:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 }
             }
        }
    }
}
