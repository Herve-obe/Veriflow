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



        public List<string> AudioFormats { get; } = new() 
        { 
            "WAV", "AIFF", "FLAC", "MP3", "AAC", "AC3", "Opus", "Vorbis" 
        };

        public List<string> VideoFormats { get; } = new() 
        { 
            "--- STANDARD (EXCHANGE) ---",
            "H.264 (MP4)", "H.265 (MP4)", 

            "--- BROADCAST & LIVE ---",
            "XDCAM HD422", "AVC-Intra 100", "XAVC", "HAP",

            "--- POST-PRODUCTION ---",
            "ProRes", "DNxHD", "DNxHR", "GoPro CineForm", "QT Animation", "Uncompressed",

            "--- MODERN WEB ---",
            "AV1", "VP9", "VP8",

            "--- LEGACY & ARCHIVE ---",
            "MPEG-2", "MPEG-1", "DV", "theora", "MJPEG", "Xvid", "WMV"
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
        private string _outputExtension = ".mp4";

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


            // Initialize

            UpdateFormatOptions(SelectedFormat);
        }

        private void UpdateFormatOptions(string? format)
        {
            if (string.IsNullOrEmpty(format)) return;

            AvailableBitrates.Clear();
            SelectedBitrate = "";

            // Determine Type
            bool isVideo = format.Contains("ProRes") || format.Contains("DNxHD") || format.Contains("DNxHR") || format.Contains("H.264") || format.Contains("H.265") || format.Contains("MP4") 
               || format.Contains("CineForm") || format.Contains("Animation") || format.Contains("Uncompressed")
               || format.Contains("VP") || format.Contains("AV1") || format.Contains("XDCAM") || format.Contains("AVC-Intra") || format.Contains("XAVC") || format.Contains("HAP")
               || format.Contains("Theora") || format.Contains("MPEG") || format.Contains("MJPEG") || format.Contains("Xvid") || format.Contains("DV") || format.Contains("WMV");
            
            IsVideoFormat = isVideo;

            if (IsVideoFormat)
            {
                // Reset Bit Depths
                AvailableVideoBitDepths.Clear();

                // --- H.264 ---
                if (format.Contains("H.264") || format.Equals("MP4"))
                {
                     AvailableBitrates.Add("Auto"); AvailableBitrates.Add("2000k"); AvailableBitrates.Add("5000k"); AvailableBitrates.Add("8000k"); AvailableBitrates.Add("10000k"); AvailableBitrates.Add("15000k"); AvailableBitrates.Add("20000k"); AvailableBitrates.Add("30000k"); AvailableBitrates.Add("50000k");
                     SelectedBitrate = "Auto";
                     
                     AvailableVideoBitDepths.Add("8-bit");
                     SelectedVideoBitDepth = "8-bit";
                }
                // --- H.265 / AV1 / VP9 (Modern High Depth) ---
                else if (format.Contains("H.265") || format.Contains("AV1") || format.Contains("VP9"))
                {
                     AvailableBitrates.Add("Auto"); AvailableBitrates.Add("2000k"); AvailableBitrates.Add("5000k"); AvailableBitrates.Add("8000k"); AvailableBitrates.Add("10000k"); AvailableBitrates.Add("15000k"); AvailableBitrates.Add("20000k"); AvailableBitrates.Add("30000k"); AvailableBitrates.Add("50000k");
                     SelectedBitrate = "Auto";

                     AvailableVideoBitDepths.Add("8-bit"); AvailableVideoBitDepths.Add("10-bit"); AvailableVideoBitDepths.Add("12-bit");
                     SelectedVideoBitDepth = "8-bit";
                }
                // --- PRORES (Fixed 10-bit) ---
                else if (format.Contains("ProRes"))
                {
                    AvailableBitrates.Add("Proxy"); AvailableBitrates.Add("LT"); AvailableBitrates.Add("422"); AvailableBitrates.Add("HQ"); AvailableBitrates.Add("4444");
                    SelectedBitrate = "422";

                    AvailableVideoBitDepths.Add("10-bit");
                    SelectedVideoBitDepth = "10-bit";
                }
                // --- DNxHD (Fixed 8/10 depending on profile, treat as generic selectable for now or assume 8 unless X) ---
                else if (format.Contains("DNxHD"))
                {
                    AvailableBitrates.Add("36M (Proxy)"); AvailableBitrates.Add("115M (SQ)"); AvailableBitrates.Add("175M (HQ)"); AvailableBitrates.Add("175X (10-bit)");
                    SelectedBitrate = "115M (SQ)";
                    
                    AvailableVideoBitDepths.Add("8-bit"); AvailableVideoBitDepths.Add("10-bit");
                    SelectedVideoBitDepth = "8-bit"; 
                }
                // --- DNxHR ---
                else if (format.Contains("DNxHR"))
                {
                    AvailableBitrates.Add("LB"); AvailableBitrates.Add("SQ"); AvailableBitrates.Add("HQ"); AvailableBitrates.Add("HQX (10-bit)"); AvailableBitrates.Add("444 (12-bit)");
                    SelectedBitrate = "SQ";

                    AvailableVideoBitDepths.Add("8-bit"); AvailableVideoBitDepths.Add("10-bit"); AvailableVideoBitDepths.Add("12-bit");
                    SelectedVideoBitDepth = "8-bit";
                }
                // --- CINEFORM ---
                else if (format.Contains("CineForm"))
                {
                    AvailableBitrates.Add("Low"); AvailableBitrates.Add("Medium"); AvailableBitrates.Add("High"); AvailableBitrates.Add("Film Scan 1"); AvailableBitrates.Add("Film Scan 2");
                    SelectedBitrate = "Medium";

                    AvailableVideoBitDepths.Add("10-bit");
                    SelectedVideoBitDepth = "10-bit";
                }
                // --- XDCAM (Fixed) ---
                else if (format.Contains("XDCAM"))
                {
                    AvailableBitrates.Add("50Mbps");
                    SelectedBitrate = "50Mbps";
                    
                    AvailableVideoBitDepths.Add("8-bit");
                    SelectedVideoBitDepth = "8-bit";
                }
                // --- AVC-INTRA / XAVC (High Depth) ---
                else if (format.Contains("AVC-Intra") || format.Contains("XAVC"))
                {
                     if (format.Contains("XAVC"))
                     {
                        AvailableBitrates.Add("100Mbps (Class 100)"); AvailableBitrates.Add("300Mbps (Class 300)"); AvailableBitrates.Add("480Mbps (Class 480)");
                        SelectedBitrate = "300Mbps (Class 300)";
                     }
                     else
                     {
                        AvailableBitrates.Add("100Mbps");
                        SelectedBitrate = "100Mbps";
                     }

                     AvailableVideoBitDepths.Add("10-bit");
                     SelectedVideoBitDepth = "10-bit";
                }
                // --- HAP ---
                else if (format.Contains("HAP"))
                {
                    AvailableBitrates.Add("Hap"); AvailableBitrates.Add("Hap Alpha"); AvailableBitrates.Add("Hap Q");
                    SelectedBitrate = "Hap";

                    AvailableVideoBitDepths.Add("8-bit");
                    SelectedVideoBitDepth = "8-bit";
                }
                // --- LEGACY / OTHER ---
                else
                {
                    // MPEG-2, DV, WMV, etc.
                    if (format.Contains("DV"))
                    {
                        AvailableBitrates.Add("Standard (25Mbps)");
                        SelectedBitrate = "Standard (25Mbps)";
                    }
                    else if (format.Contains("MPEG-2"))
                    {
                        AvailableBitrates.Add("5000k"); AvailableBitrates.Add("10000k"); AvailableBitrates.Add("15000k"); AvailableBitrates.Add("30000k"); AvailableBitrates.Add("50000k");
                        SelectedBitrate = "15000k";
                    }
                    else
                    {
                        // Generic Legacy (Theora, Xvid, WMV, VP8, MJPEG)
                        AvailableBitrates.Add("Auto"); AvailableBitrates.Add("2000k"); AvailableBitrates.Add("5000k"); AvailableBitrates.Add("10000k");
                        SelectedBitrate = "Auto";
                    }

                    AvailableVideoBitDepths.Add("8-bit");
                    SelectedVideoBitDepth = "8-bit";
                    
                    if (format == "Uncompressed")
                    {
                         AvailableBitrates.Clear();
                         AvailableBitrates.Add("YUV 4:2:2"); AvailableBitrates.Add("RGB 24-bit"); AvailableBitrates.Add("RGBA 32-bit");
                         SelectedBitrate = "YUV 4:2:2";
                         
                         AvailableVideoBitDepths.Clear();
                         AvailableVideoBitDepths.Add("8-bit"); AvailableVideoBitDepths.Add("10-bit"); AvailableVideoBitDepths.Add("12-bit");
                         SelectedVideoBitDepth = "8-bit";
                    }
                }

                // --- Dynamic Container Logic ---
                if (format.Contains("ProRes") || format.Contains("DNxHD") || format.Contains("DNxHR") || format.Contains("Animation") || format.Contains("CineForm") || format.Contains("Uncompressed")) OutputExtension = ".mov";
                else if (format.Contains("HAP") || format.Contains("MJPEG")) OutputExtension = ".mov";
                else if (format.Contains("XDCAM") || format.Contains("AVC-Intra") || format.Contains("XAVC")) OutputExtension = ".mxf";
                else if (format.Contains("VP8") || format.Contains("VP9")) OutputExtension = ".webm";
                else if (format.Contains("Theora")) OutputExtension = ".ogv";
                else if (format.Contains("WMV")) OutputExtension = ".wmv";
                else if (format.Contains("Xvid")) OutputExtension = ".avi";
                else if (format.Contains("DV")) OutputExtension = ".dv";
                else if (format.Contains("MPEG-2") || format.Contains("MPEG-1")) OutputExtension = ".mpg";
                else OutputExtension = ".mp4"; // Default
            }
            else
            {
                // Audio Logic
                if (format == "MP3")
                {
                    AvailableBitrates.Add("320k"); AvailableBitrates.Add("256k"); AvailableBitrates.Add("192k"); AvailableBitrates.Add("128k");
                    SelectedBitrate = "320k";
                    OutputExtension = ".mp3";
                }
                else if (format == "AAC")
                {
                    AvailableBitrates.Add("320k"); AvailableBitrates.Add("256k"); AvailableBitrates.Add("192k"); AvailableBitrates.Add("128k");
                    SelectedBitrate = "256k";
                    OutputExtension = ".m4a";
                }
                else if (format == "AC3")
                {
                    AvailableBitrates.Add("640k"); AvailableBitrates.Add("448k"); AvailableBitrates.Add("384k"); AvailableBitrates.Add("192k");
                    SelectedBitrate = "384k";
                    OutputExtension = ".ac3";
                }
                else if (format == "Opus")
                {
                    AvailableBitrates.Add("320k"); AvailableBitrates.Add("256k"); AvailableBitrates.Add("192k"); AvailableBitrates.Add("160k"); AvailableBitrates.Add("128k"); AvailableBitrates.Add("96k"); AvailableBitrates.Add("64k");
                    SelectedBitrate = "128k"; // Opus default quality is good at lower rates
                    OutputExtension = ".opus";
                }
                else if (format == "Vorbis")
                {
                    AvailableBitrates.Add("320k"); AvailableBitrates.Add("256k"); AvailableBitrates.Add("192k"); AvailableBitrates.Add("128k"); AvailableBitrates.Add("96k");
                    SelectedBitrate = "192k";
                    OutputExtension = ".ogg";
                }
                else if (format == "WAV") OutputExtension = ".wav";
                else if (format == "AIFF") OutputExtension = ".aif";
                else if (format == "FLAC") OutputExtension = ".flac";
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
                    
                    // Extension Logic (Centralized from Property)
                    string extension = OutputExtension.TrimStart('.'); 

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

