using CSCore;
using CSCore.Codecs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using Veriflow.Desktop.Services;

namespace Veriflow.Desktop.ViewModels
{
    public enum ExportCategory { Audio, Video }

    public partial class TranscodeViewModel : ObservableObject
    {
        private readonly ITranscodingService _transcodingService;

        public ObservableCollection<TranscodeItemViewModel> Files { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartTranscodeCommand))]
        [NotifyCanExecuteChangedFor(nameof(ClearListCommand))]
        private bool _isBusy;

        [ObservableProperty]
        private double _totalProgressValue;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        // Category Support
        [ObservableProperty]
        private ExportCategory _selectedCategory = ExportCategory.Audio;



        // Formats
        public List<string> AudioFormats { get; } = new() 
        { 
            "WAV", "FLAC", "MP3", "AAC", "OGG", "AIFF" 
        };

        public List<string> VideoFormats { get; } = new() 
        { 
            "H.264 (MP4)", "H.265 (MP4)",
            "ProRes 422 Proxy", "ProRes 422 LT", "ProRes 422", "ProRes 422 HQ", "ProRes 4444",
            "DNxHD LB", "DNxHD SQ", "DNxHD HQ"
        };
        
        // No longer using single list
        // public ObservableCollection<string> AvailableFormats { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAudioFormat))]
        [NotifyPropertyChangedFor(nameof(IsVideoFormat))]
        [NotifyPropertyChangedFor(nameof(IsAudioCategory))]
        [NotifyPropertyChangedFor(nameof(IsVideoCategory))]
        private string? _selectedFormat; // Default is null (no selection)

        [ObservableProperty]
        private string? _selectedAudioFormat;

        [ObservableProperty]
        private string? _selectedVideoFormat;

        partial void OnSelectedAudioFormatChanged(string? value)
        {
            if (value != null)
            {
                SelectedVideoFormat = null; // Clear Video Logic
                SelectedFormat = value;
                SelectedCategory = ExportCategory.Audio;
            }
            // If value is null, we can choose to clear SelectedFormat or not.
            // If the user clears the combo (if possible), we should clear Main SelectedFormat
            else if (SelectedVideoFormat == null)
            {
                SelectedFormat = null;
            }
        }

        partial void OnSelectedVideoFormatChanged(string? value)
        {
            if (value != null)
            {
                SelectedAudioFormat = null; // Clear Audio Logic
                SelectedFormat = value;
                SelectedCategory = ExportCategory.Video;
            }
            else if (SelectedAudioFormat == null)
            {
                SelectedFormat = null;
            }
        }

        [ObservableProperty]
        private bool _isVideoFormat; 

        public bool IsAudioFormat => !IsVideoFormat;
        
        // Settings Visibility: Only show if Category matches AND a format is selected
        public bool IsAudioCategory => SelectedCategory == ExportCategory.Audio && !string.IsNullOrEmpty(SelectedFormat);
        public bool IsVideoCategory => SelectedCategory == ExportCategory.Video && !string.IsNullOrEmpty(SelectedFormat);

        partial void OnSelectedFormatChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value)) UpdateFormatOptions(value);
        }

        partial void OnSelectedCategoryChanged(ExportCategory value)
        {
             // When Category Changes, strictly notify UI.
             // Do NOT auto-select a format. User must choose.
             
             OnPropertyChanged(nameof(IsAudioCategory));
             OnPropertyChanged(nameof(IsVideoCategory));
        }

        [RelayCommand]
        private void SelectCategory(string category)
        {
            if (Enum.TryParse(category, true, out ExportCategory result))
            {
                SelectedCategory = result;
            }
        }

        // Removed UpdateAvailableFormats() as it's not needed for Split Dropdowns

        // Bitrates Support
        public ObservableCollection<string> AvailableBitrates { get; } = new();

        [ObservableProperty]
        private string _selectedBitrate = "";

        // ... Sample Rates, Bit Depths ... (lines 62-87 from original are fine, keeping context)
        
        // Audio Bit Depths
        public ObservableCollection<string> AvailableSampleRates { get; } = new()
        {
            "Same as Source", "44100", "48000", "88200", "96000", "192000"
        };

        [ObservableProperty]
        private string _selectedSampleRate = "Same as Source";

        public ObservableCollection<string> AvailableBitDepths { get; } = new()
        {
            "Same as Source", "16-bit", "24-bit", "32-bit Float"
        };

        [ObservableProperty]
        private string _selectedBitDepth = "Same as Source";

        // Video Bit Depths
        public ObservableCollection<string> AvailableVideoBitDepths { get; } = new()
        {
            "Same as Source", "8-bit", "10-bit"
        };

        [ObservableProperty]
        private string _selectedVideoBitDepth = "Same as Source";

        [ObservableProperty]
        private string _destinationFolder = "";

        public TranscodeViewModel()
        {
            _transcodingService = new Services.TranscodingService();
            Files.CollectionChanged += (s, e) => 
            {
                StartTranscodeCommand.NotifyCanExecuteChanged();
                ClearListCommand.NotifyCanExecuteChanged();
            };
            
            // Initialize

            UpdateFormatOptions(SelectedFormat);
        }

        private void UpdateFormatOptions(string? format)
        {
            if (string.IsNullOrEmpty(format)) return;

            AvailableBitrates.Clear();
            SelectedBitrate = "";

            // Determine Type based on Format Name String
            bool isVideo = format.Contains("ProRes") || format.Contains("DNxHD") || format.Contains("H.264") || format.Contains("H.265") || format.Contains("MP4");
            IsVideoFormat = isVideo; // Updates UI Flags

            if (IsVideoFormat)
            {
                // Video Specific Defaults
                // ... (Logic preserved)
                if (SelectedVideoBitDepth == "Same as Source" && format.Contains("ProRes")) SelectedVideoBitDepth = "10-bit";
            }
            else
            {
                // Audio Logic
                if (format == "MP3")
                {
                    AvailableBitrates.Add("320k"); AvailableBitrates.Add("256k"); AvailableBitrates.Add("192k"); AvailableBitrates.Add("128k");
                    SelectedBitrate = "320k";
                }
                else if (format == "AAC")
                {
                    AvailableBitrates.Add("320k"); AvailableBitrates.Add("256k"); AvailableBitrates.Add("192k");
                    SelectedBitrate = "256k";
                }
                else if (format == "OGG")
                {
                     AvailableBitrates.Add("192k"); AvailableBitrates.Add("128k");
                     SelectedBitrate = "192k";
                }
            }
        }

        [RelayCommand]
        private void DropFiles(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        // Filter explicit audio extensions if needed, or allow all and let ffmpeg handle
                        AddFile(file);
                    }
                }
            }
        }

        [RelayCommand]
        private void AddFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Media Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach(var file in dialog.FileNames)
                {
                    AddFile(file);
                }
            }
        }

        public void AddFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                AddFile(path);
            }
        }

        private void AddFile(string path)
        {
            // Avoid duplicates?
            if (!Files.Any(f => f.FilePath == path))
            {
                var item = new TranscodeItemViewModel(path);
                Files.Add(item);
                
                // Async Metadata Load (Fire and Forget)
                _ = LoadItemMetadataAsync(item);
            }
        }

        private async Task LoadItemMetadataAsync(TranscodeItemViewModel item)
        {
            try 
            {
                item.Status = "Analyzing...";
                var meta = await _transcodingService.GetMediaMetadataAsync(item.FilePath);
                
                // Update on UI Thread (Observable properties handle notification)
                item.DurationString = meta.Duration.ToString(@"mm\:ss");
                
                string info = meta.CodecInfo;
                if (meta.SampleRate > 0) info += $" | {meta.SampleRate}Hz";
                item.AudioInfo = info; // Reusing AudioInfo property for general format info
                
                item.Status = "Pending";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Metadata Error: {ex}");
                item.AudioInfo = "Unknown Format";
                item.Status = "Ready"; // Assume ready anyway
            }
        }

        [RelayCommand]
        private void RemoveFile(TranscodeItemViewModel item)
        {
            if (Files.Contains(item))
            {
                Files.Remove(item);
            }
        }

        [RelayCommand(CanExecute = nameof(CanClearList))]
        private void ClearList()
        {
            Files.Clear();
        }

        [RelayCommand]
        private void PickDestination()
        {
             var dialog = new Microsoft.Win32.OpenFolderDialog(); 
             if (dialog.ShowDialog() == true)
             {
                 DestinationFolder = dialog.FolderName;
             }
        }

        [RelayCommand(CanExecute = nameof(CanStartTranscode))]
        private async Task StartTranscode()
        {
            if (IsBusy) return;
            if (Files.Count == 0) return;

            if (string.IsNullOrEmpty(SelectedFormat))
            {
                StatusMessage = "Please select a format.";
                return;
            }
            string currentFormat = SelectedFormat;

            IsBusy = true;
            StatusMessage = "Processing...";
            TotalProgressValue = 0;

            int processedCount = 0;
            int totalCount = Files.Count;

            try
            {
                var options = new TranscodeOptions
                {
                    Format = currentFormat,
                    SampleRate = SelectedSampleRate,
                    BitDepth = SelectedBitDepth,
                    Bitrate = SelectedBitrate,
                    VideoBitDepth = SelectedVideoBitDepth
                };

                foreach (var item in Files)
                {
                    item.Status = "Processing";
                    
                    // Determine output path
                    string? rawDir = string.IsNullOrEmpty(DestinationFolder) ? System.IO.Path.GetDirectoryName(item.FilePath) : DestinationFolder;
                    string outDir = rawDir ?? ""; 
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(item.FilePath);
                    
                    // Extension Logic
                    string extension = "wav"; // fallback
                    if (currentFormat.Contains("MP3")) extension = "mp3";
                    else if (currentFormat.Contains("AAC")) extension = "m4a";
                    else if (currentFormat.Contains("WAV")) extension = "wav";
                    else if (currentFormat.Contains("FLAC")) extension = "flac";
                    else if (currentFormat.Contains("OGG")) extension = "ogg";
                    else if (currentFormat.Contains("AIFF")) extension = "aif";
                    else if (currentFormat.Contains("H.264") || currentFormat.Contains("H.265")) extension = "mp4";
                    else if (currentFormat.Contains("ProRes") || currentFormat.Contains("DNxHD")) extension = "mov";

                    string outputFile = System.IO.Path.Combine(outDir, $"{fileName}_{currentFormat.Split(' ')[0].ToUpper()}.{extension}");

                    // Avoid overwrite source
                    if (outputFile == item.FilePath)
                    {
                        outputFile = System.IO.Path.Combine(outDir, $"{fileName}_Transcoded.{extension}");
                    }

                    try
                    {
                        await _transcodingService.TranscodeAsync(item.FilePath, outputFile, options, null);
                        item.Status = "Done";
                        item.ProgressValue = 100;
                    }
                    catch (System.Exception itemEx)
                    {
                        item.Status = "Error";
                        if (itemEx.Message.Contains("Find the file") || itemEx is System.ComponentModel.Win32Exception)
                        {
                            MessageBox.Show($"FFmpeg not found. Please ensure FFmpeg is installed and in your PATH.\nError: {itemEx.Message}", "Dependency Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                            IsBusy = false;
                            return;
                        }
                        System.Diagnostics.Debug.WriteLine($"Error transcoding {item.FilePath}: {itemEx}");
                    }

                    processedCount++;
                    TotalProgressValue = (double)processedCount / totalCount * 100;
                }
                
                StatusMessage = "Batch Completed!";
            }
            catch (System.Exception ex)
            {
                StatusMessage = "Global Error";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanStartTranscode()
        {
            return Files.Count > 0 && !IsBusy;
        }

        private bool CanClearList()
        {
             return Files.Count > 0 && !IsBusy;
        }
    }
}

