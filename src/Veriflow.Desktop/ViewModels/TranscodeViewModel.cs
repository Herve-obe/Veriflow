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

        // Bitrates Support
        public ObservableCollection<string> AvailableBitrates { get; } = new();

        [ObservableProperty]
        private string _selectedBitrate = "";

        // Settings
        public ObservableCollection<string> AvailableFormats { get; } = new()
        {
            "WAV", "FLAC", "MP3", "AAC", "OGG", "AIFF"
        };

        [ObservableProperty]
        private string _selectedFormat = "WAV";

        partial void OnSelectedFormatChanged(string value)
        {
            UpdateBitrates(value);
        }

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
            Files.CollectionChanged += (s, e) => 
            {
                StartTranscodeCommand.NotifyCanExecuteChanged();
                ClearListCommand.NotifyCanExecuteChanged();
            };
            
            // Initialize bitrates for default format
            UpdateBitrates(SelectedFormat);
        }

        private void UpdateBitrates(string format)
        {
            AvailableBitrates.Clear();
            SelectedBitrate = "";

            if (format == "MP3")
            {
                AvailableBitrates.Add("320k");
                AvailableBitrates.Add("256k");
                AvailableBitrates.Add("192k");
                AvailableBitrates.Add("128k");
                AvailableBitrates.Add("96k");
                AvailableBitrates.Add("64k");
                SelectedBitrate = "320k";
            }
            else if (format == "AAC")
            {
                AvailableBitrates.Add("320k");
                AvailableBitrates.Add("256k");
                AvailableBitrates.Add("192k");
                AvailableBitrates.Add("128k");
                AvailableBitrates.Add("64k");
                SelectedBitrate = "256k";
            }
            else if (format == "OGG")
            {
                 AvailableBitrates.Add("320k");
                 AvailableBitrates.Add("192k");
                 AvailableBitrates.Add("128k");
                 AvailableBitrates.Add("96k");
                 SelectedBitrate = "192k";
            }
            else
            {
                // WAV, FLAC, AIFF - No bitrate selection (PCM or Lossless)
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
                
                // Try to read metadata
                try
                {
                    using (var source = CSCore.Codecs.CodecFactory.Instance.GetCodec(path))
                    {
                        var time = source.GetLength();
                        item.DurationString = time.ToString(@"mm\:ss");
                        item.AudioInfo = $"{source.WaveFormat.SampleRate}Hz | {source.WaveFormat.BitsPerSample}-bit";
                    }
                }
                catch
                {
                    item.AudioInfo = "Unknown Format";
                }

                Files.Add(item);
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

            IsBusy = true;
            StatusMessage = "Processing...";
            TotalProgressValue = 0;

            int processedCount = 0;
            int totalCount = Files.Count;

            try
            {

                var options = new TranscodeOptions
                {
                    Format = SelectedFormat,
                    SampleRate = SelectedSampleRate,
                    BitDepth = SelectedBitDepth,
                    Bitrate = SelectedBitrate
                };

                foreach (var item in Files)
                {
                    item.Status = "Processing";
                    
                    // Determine output path
                    string? rawDir = string.IsNullOrEmpty(DestinationFolder) ? System.IO.Path.GetDirectoryName(item.FilePath) : DestinationFolder;
                    string outDir = rawDir ?? ""; 
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(item.FilePath);
                    string extension = SelectedFormat.ToLower();
                    // Handle AIFF extension
                    if (extension == "aiff") extension = "aif";
                    
                    string outputFile = System.IO.Path.Combine(outDir, $"{fileName}_{extension.ToUpper()}.{extension}");

                    // Make sure we don't overwrite source if same name (rare due to extension change, but possible)
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
                        // If it's a missing executable, we should probably stop and warn
                        if (itemEx.Message.Contains("Find the file") || itemEx is System.ComponentModel.Win32Exception)
                        {
                            MessageBox.Show($"FFmpeg not found. Please ensure FFmpeg is installed and in your PATH.\nError: {itemEx.Message}", "Dependency Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                            IsBusy = false;
                            return;
                        }
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

