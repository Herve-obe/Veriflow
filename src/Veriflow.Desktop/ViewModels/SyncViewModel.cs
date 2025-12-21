using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Veriflow.Desktop.Models;
using Veriflow.Desktop.Services;

namespace Veriflow.Desktop.ViewModels
{
    public enum TimecodeSource
    {
        Metadata,
        LTC
    }

    public enum SyncMethod
    {
        Timecode,   // Synced via timecode comparison
        Waveform,   // Synced via audio waveform correlation
        Manual      // Manually paired by user
    }

    public partial class SyncViewModel : ObservableObject
    {
        private readonly FFprobeMetadataProvider _metadataProvider;
        private readonly WaveformSyncService _waveformService;

        public SyncViewModel()
        {
            _metadataProvider = new FFprobeMetadataProvider();
            _waveformService = new WaveformSyncService();
        }

        public static IEnumerable<TimecodeSource> TimecodeSources => Enum.GetValues(typeof(TimecodeSource)).Cast<TimecodeSource>();

        // ==========================================
        // SYNC METHOD SELECTION
        // ==========================================
        [ObservableProperty]
        private SyncMethod _selectedSyncMethod = SyncMethod.Timecode;

        // ==========================================
        // COLLECTIONS
        // ==========================================
        [ObservableProperty]
        private ObservableCollection<MediaFile> _videoPool = new();

        [ObservableProperty]
        private ObservableCollection<MediaFile> _audioPool = new();

        [ObservableProperty]
        private ObservableCollection<SyncPair> _matches = new();

        [ObservableProperty]
        private SyncPair? _selectedMatch;

        // ==========================================
        // STATE & PROGRESS
        // ==========================================
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _busyMessage = "Processing...";

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExportBatchCommand))]
        private string _exportDestination = "";

        // ==========================================
        // HELPERS
        // ==========================================
        public bool HasMatches => Matches.Count > 0;

        // ==========================================
        // COMMANDS
        // ==========================================

        [RelayCommand]
        private async Task ImportVideo()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Video Rushes",
                Filter = "Video Files|*.mp4;*.mov;*.mxf;*.avi;*.mkv|All Files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                await IngestFiles(dialog.FileNames, isVideo: true);
            }
        }

        [RelayCommand]
        private async Task ImportAudio()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Audio Rushes",
                Filter = "Audio Files|*.wav;*.bwf;*.mp3;*.m4a;*.flac|All Files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                await IngestFiles(dialog.FileNames, isVideo: false);
            }
        }

        [RelayCommand]
        private async Task DropVideo(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                await IngestFiles(paths, isVideo: true);
            }
        }

        [RelayCommand]
        private async Task DropAudio(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                await IngestFiles(paths, isVideo: false);
            }
        }

        [RelayCommand]
        private void ClearVideoPool()
        {
            VideoPool.Clear();
            Matches.Clear();
            OnPropertyChanged(nameof(HasMatches));
            // Force UI update for commands if needed, although View triggers handle color.
        }

        [RelayCommand]
        private void ClearAudioPool()
        {
            AudioPool.Clear();
            Matches.Clear();
            OnPropertyChanged(nameof(HasMatches));
        }

        [RelayCommand]
        private void RemoveVideoItem(MediaFile file)
        {
            if (VideoPool.Contains(file))
            {
                VideoPool.Remove(file);
                // Re-sync or clear matches involving this file?
                // Simplest: Clear matches and let user see they need to resync or auto-remove matches?
                // Logic: If we remove a source file, any match using it is invalid.
                var toRemove = Matches.Where(m => m.Video == file).ToList();
                foreach (var m in toRemove) Matches.Remove(m);
                OnPropertyChanged(nameof(HasMatches));
            }
        }

        [RelayCommand]
        private void RemoveAudioItem(MediaFile file)
        {
             if (AudioPool.Contains(file))
            {
                AudioPool.Remove(file);
                var toRemove = Matches.Where(m => m.Audio == file).ToList();
                foreach (var m in toRemove) Matches.Remove(m);
                OnPropertyChanged(nameof(HasMatches));
            }
        }

        [RelayCommand]
        private void RemoveMatchItem(object? item)
        {
            if (item is SyncPair match && Matches.Contains(match))
            {
                Matches.Remove(match);
                OnPropertyChanged(nameof(HasMatches));
            }
        }

        [RelayCommand]
        private void ClearAll()
        {
            VideoPool.Clear();
            AudioPool.Clear();
            Matches.Clear();
            OnPropertyChanged(nameof(HasMatches));
        }

        [RelayCommand]
        private void SelectExportFolder()
        {
             var dialog = new Microsoft.Win32.SaveFileDialog
             {
                 Title = "Select Export Destination (Save any file here)",
                 FileName = "Select_Folder_Here",
                 CheckPathExists = true
             };
             
             if (dialog.ShowDialog() == true)
             {
                 ExportDestination = System.IO.Path.GetDirectoryName(dialog.FileName) ?? "";
             }
        }

        private bool CanExportBatch() => Matches.Count > 0 && !string.IsNullOrEmpty(ExportDestination);

        [RelayCommand(CanExecute = nameof(CanExportBatch))]
        private async Task ExportBatch()
        {
            if (!CanExportBatch()) return;
            
            IsBusy = true;
            BusyMessage = "Exporting Batch...";
            ProgressValue = 0;
            
            int total = Matches.Count;
            int current = 0;
            
            string ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");

            await Task.Run(async () =>
            {
                foreach (var match in Matches)
                {
                    if (match.Status != "Synced") continue; 

                    current++;
                    BusyMessage = $"Exporting {current}/{total}: {match.Video.Filename}";
                    ProgressValue = (double)current / total * 100;

                    string outName = $"{System.IO.Path.GetFileNameWithoutExtension(match.Video.Filename)}_SYNC MASTER.mov";
                    string outPath = System.IO.Path.Combine(ExportDestination, outName);
                    
                    await RunExport(match, outPath, ffmpegPath);
                    match.Status = "Exported";
                }
            });
            
            IsBusy = false;
            MessageBox.Show("Batch Export Complete!", "Veriflow Sync", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ==========================================
        // LOGIC
        // ==========================================

        private async Task IngestFiles(string[] paths, bool isVideo)
        {
            IsBusy = true;
            BusyMessage = isVideo ? "Scanning Videos..." : "Scanning Audio...";
            ProgressValue = 0;

            var validExts = isVideo 
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".mxf", ".avi", ".mkv" }
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wav", ".bwf", ".mp3", ".m4a", ".flac" };

            var list = new List<string>();
            
            await Task.Run(() => 
            {
                foreach (var p in paths)
                {
                    if (System.IO.Directory.Exists(p))
                    {
                        try 
                        {
                            var files = System.IO.Directory.GetFiles(p, "*.*", System.IO.SearchOption.AllDirectories);
                            foreach (var f in files)
                            {
                                if (validExts.Contains(System.IO.Path.GetExtension(f)))
                                {
                                    list.Add(f);
                                }
                            }
                        }
                        catch {}
                    }
                    else if (System.IO.File.Exists(p))
                    {
                         if (validExts.Contains(System.IO.Path.GetExtension(p)))
                         {
                             list.Add(p);
                         }
                    }
                }
            });

            int count = 0;
            int total = list.Count;
            
            if (total == 0) 
            {
                IsBusy = false;
                return;
            }

            foreach (var file in list)
            {
                count++;
                ProgressValue = (double)count / total * 100;
                BusyMessage = $"Parsing {System.IO.Path.GetFileName(file)}...";

                try
                {
                    var mediaFile = new MediaFile { FullPath = file, Filename = System.IO.Path.GetFileName(file) };
                    
                    if (isVideo)
                    {
                        var meta = await _metadataProvider.GetVideoMetadataAsync(file);
                        mediaFile.Duration = meta.Duration;
                        mediaFile.StartTimecode = meta.StartTimecode ?? "00:00:00:00";
                        
                        double fps = 25;
                        ParseFps(meta.FrameRate, ref fps);
                        mediaFile.Fps = fps;
                        
                        mediaFile.StartSeconds = TimecodeToSeconds(mediaFile.StartTimecode, fps);
                        double durSec = 0;
                        if (TimeSpan.TryParse(meta.Duration, out var ts)) durSec = ts.TotalSeconds;
                        mediaFile.EndSeconds = mediaFile.StartSeconds + durSec;

                         Application.Current.Dispatcher.Invoke(() => VideoPool.Add(mediaFile));
                    }
                    else
                    {
                        var meta = await _metadataProvider.GetMetadataAsync(file);
                        mediaFile.Duration = meta.Duration;
                        mediaFile.StartTimecode = meta.TimecodeStart ?? "00:00:00:00";
                        
                        double fps = 25;
                        ParseFps(meta.FrameRate, ref fps);
                        mediaFile.Fps = fps;

                         mediaFile.StartSeconds = meta.TimeReferenceSeconds; 
                         
                         double durSec = 0;
                         if (TimeSpan.TryParse(meta.Duration, out var ts)) durSec = ts.TotalSeconds;
                         mediaFile.EndSeconds = mediaFile.StartSeconds + durSec;

                         Application.Current.Dispatcher.Invoke(() => AudioPool.Add(mediaFile));
                    }
                }
                catch { }
            }
            
            IsBusy = false;
            
            // Removed auto-sync - let user choose sync method manually
        }

        [RelayCommand]
        private async Task AutoMatchByTimecode()
        {
            SelectedSyncMethod = SyncMethod.Timecode;
            await AutoSync();
        }

        [RelayCommand]
        private async Task AutoMatchByWaveform()
        {
            SelectedSyncMethod = SyncMethod.Waveform;
            await AutoSync();
        }

        private async Task AutoSync()
        {
            IsBusy = true;
            BusyMessage = SelectedSyncMethod == SyncMethod.Timecode 
                ? "Auto-Matching by Timecode..." 
                : "Auto-Matching by Waveform...";
            ProgressValue = 0;
            
            // Don't clear matches - we'll add to existing ones
            // Application.Current.Dispatcher.Invoke(() => Matches.Clear());

            if (SelectedSyncMethod == SyncMethod.Timecode)
            {
                await AutoSyncByTimecode();
            }
            else if (SelectedSyncMethod == SyncMethod.Waveform)
            {
                await AutoSyncByWaveform();
            }

            IsBusy = false;
            OnPropertyChanged(nameof(HasMatches));
            ExportBatchCommand.NotifyCanExecuteChanged();
        }

        private async Task AutoSyncByTimecode()
        {
            await Task.Run(() =>
            {
                // Get list of videos already matched by other methods
                var alreadyMatchedVideos = Matches
                    .Where(m => m.Status == "Synced" && m.SyncMethod != SyncMethod.Timecode)
                    .Select(m => m.Video)
                    .ToHashSet();

                // Filter to only unmatched videos
                var videosToProcess = VideoPool.Where(v => !alreadyMatchedVideos.Contains(v)).ToList();

                if (videosToProcess.Count == 0)
                {
                    return; // All videos already matched
                }


                // Algorithm:
                // Iterate Videos. Find matching Audios.
                int processed = 0;
                
                foreach (var vid in videosToProcess)
                {
                    // Find audio that overlaps
                    // AudioStart < VideoEnd AND AudioEnd > VideoStart
                    
                    var candidates = AudioPool.Where(a => 
                        a.StartSeconds < vid.EndSeconds && 
                        a.EndSeconds > vid.StartSeconds
                    ).ToList();

                    if (candidates.Any())
                    {
                        // Strategy: Add pairwise.
                        foreach (var aud in candidates)
                        {
                            var pair = new SyncPair
                            {
                                Video = vid,
                                Audio = aud,
                                Status = "Synced",
                                SyncMethod = SyncMethod.Timecode
                            };
                            
                            // Calculate Offset
                            // Delta = Audio - Video
                            double diff = aud.StartSeconds - vid.StartSeconds;
                            pair.OffsetSeconds = diff;
                            pair.OffsetDisplay = FormatTimecode(TimeSpan.FromSeconds(Math.Abs(diff)), vid.Fps);
                            if (diff > 0) pair.OffsetDisplay = "+ " + pair.OffsetDisplay;
                            else pair.OffsetDisplay = "- " + pair.OffsetDisplay;

                            Application.Current.Dispatcher.Invoke(() => Matches.Add(pair));
                        }
                    }
                    else
                    {
                        // No match found
                        Application.Current.Dispatcher.Invoke(() => Matches.Add(new SyncPair { Video = vid, Status = "No Match" }));
                    }
                    
                    processed++;
                    ProgressValue = (double)processed / videosToProcess.Count * 100;
                }
            });
        }

        private async Task AutoSyncByWaveform()
        {
            // 1. Identify videos to process (Smart Sync: ignore already synced)
            var alreadyMatchedVideos = Matches
                .Where(m => m.Status == "Synced" && m.SyncMethod != SyncMethod.Waveform)
                .Select(m => m.Video)
                .ToHashSet();

            var videosToProcess = VideoPool.Where(v => !alreadyMatchedVideos.Contains(v)).ToList();

            if (videosToProcess.Count == 0)
            {
                IsBusy = false;
                return;
            }

            int totalOps = videosToProcess.Count + AudioPool.Count;
            int currentOp = 0;
            
            // Dictionnaries to cache extracted WAV paths
            // Key: FullPath, Value: TempWavPath
            var videoWavCache = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
            var audioWavCache = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

            try
            {
                // 2. Prepare Audio Files (Caching Phase)
                // Extract audio from ALL candidates first. This avoids O(N*M) extractions.
                
                // Prepare Videos
                foreach (var vid in videosToProcess)
                {
                    currentOp++;
                    BusyMessage = $"Preparing Video Audio ({currentOp}/{totalOps}): {vid.Filename}";
                    ProgressValue = (double)currentOp / totalOps * 50; // First 50% is prep

                    try
                    {
                        string wav = await _waveformService.PrepareAudioAsync(vid.FullPath, true, 10); // 10s analysis
                        videoWavCache.TryAdd(vid.FullPath, wav);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error preparing video {vid.Filename}: {ex.Message}");
                    }
                }

                // Prepare Audios
                foreach (var aud in AudioPool)
                {
                    currentOp++;
                    BusyMessage = $"Preparing Audio ({currentOp}/{totalOps}): {aud.Filename}";
                    ProgressValue = (double)currentOp / totalOps * 50;

                    try
                    {
                        string wav = await _waveformService.PrepareAudioAsync(aud.FullPath, false, 10);
                        audioWavCache.TryAdd(aud.FullPath, wav);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error preparing audio {aud.Filename}: {ex.Message}");
                    }
                }

                // 3. Parallel Correlation Phase
                // Compare every video against every audio using the cached WAVs
                
                BusyMessage = "Analyzing Waveforms (Parallel Processing)...";
                double totalComparisons = videosToProcess.Count;
                int processedComparisons = 0;

                // Configure parallelism (leave some cores free for UI)
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };

                await Parallel.ForEachAsync(videosToProcess, parallelOptions, async (vid, ct) =>
                {
                    if (!videoWavCache.TryGetValue(vid.FullPath, out string? vidWav)) return; // Skip if prep failed

                    SyncPair? bestPair = null;
                    double bestCorr = double.MaxValue; // Not used yet, taking first match

                    foreach (var aud in AudioPool)
                    {
                        if (!audioWavCache.TryGetValue(aud.FullPath, out string? audWav)) continue;

                        // Perform FAST correlation on memory mapped files / small WAVs
                        double? offset = await _waveformService.CalculateOffsetAsync(vidWav, audWav);

                        if (offset.HasValue)
                        {
                            // Match Found!
                            var pair = new SyncPair
                            {
                                Video = vid,
                                Audio = aud,
                                Status = "Synced",
                                SyncMethod = SyncMethod.Waveform,
                                OffsetSeconds = offset.Value
                            };
                            
                            // Format Display
                            pair.OffsetDisplay = FormatTimecode(TimeSpan.FromSeconds(Math.Abs(offset.Value)), vid.Fps);
                            if (offset.Value > 0) pair.OffsetDisplay = "+ " + pair.OffsetDisplay;
                            else pair.OffsetDisplay = "- " + pair.OffsetDisplay;

                            bestPair = pair;
                            break; // Stop after first match (Optimize later for best fit if needed)
                        }
                    }

                    // UI Update must be on Dispatcher
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (bestPair != null)
                            Matches.Add(bestPair);
                        else
                            Matches.Add(new SyncPair { Video = vid, Status = "No Match" });
                    });

                    System.Threading.Interlocked.Increment(ref processedComparisons);
                    double progress = 50 + ((double)processedComparisons / totalComparisons * 50); // Second 50%
                    Application.Current.Dispatcher.Invoke(() => ProgressValue = progress);
                });

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sync Error: {ex.Message}", "Error");
            }
            finally
            {
                // 4. Cleanup Temp Files
                BusyMessage = "Cleaning up...";
                foreach (var path in videoWavCache.Values) DeleteFileSafe(path);
                foreach (var path in audioWavCache.Values) DeleteFileSafe(path);
                
                IsBusy = false;
            }
        }

        private void DeleteFileSafe(string path)
        {
            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { }
        }
        
        private async Task RunExport(SyncPair match, string outPath, string ffmpegPath)
        {
             if (match.Audio == null || !System.IO.File.Exists(match.Audio.FullPath)) return;

             double delay = match.OffsetSeconds;
             string audioFilter = "";
             string seekInput = "";

             if (delay > 0) 
             {
                 int ms = (int)(delay * 1000);
                 audioFilter = $"-filter_complex \"[1:a]adelay={ms}|{ms}[a]\" -map 0:v -map \"[a]\"";
             }
             else
             {
                 double skip = Math.Abs(delay);
                 seekInput = $"-ss {skip:0.000}"; 
                 audioFilter = "-map 0:v -map 1:a";
             }

             string args = $"-i \"{match.Video.FullPath}\" {seekInput} -i \"{match.Audio.FullPath}\" {audioFilter} -c:v copy -c:a pcm_s24le -y \"{outPath}\"";

             var p = new System.Diagnostics.Process 
             {
                 StartInfo = new System.Diagnostics.ProcessStartInfo
                 {
                     FileName = ffmpegPath,
                     Arguments = args,
                     UseShellExecute = false,
                     CreateNoWindow = true
                 }
             };
             p.Start();
             await p.WaitForExitAsync();
        }

        // Shared Logic (Duplicated for simplicity inside this refactor)
        private void ParseFps(string fpsStr, ref double targetField)
        {
             if (string.IsNullOrEmpty(fpsStr)) return;
             string normalized = fpsStr.Replace(',', '.');
             string digits = new string(normalized.Where(c => char.IsDigit(c) || c == '.').ToArray());
             if (double.TryParse(digits, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
             {
                 if (val > 0 && val < 120) targetField = val;
             }
        }

        private double TimecodeToSeconds(string tc, double fps)
        {
            try {
                var parts = tc.Split(':');
                if (parts.Length != 4) return 0;
                int h = int.Parse(parts[0]);
                int m = int.Parse(parts[1]);
                int s = int.Parse(parts[2]);
                int f = int.Parse(parts[3]);
                return (h * 3600) + (m * 60) + s + (f / fps);
            } catch { return 0; }
        }

        private string FormatTimecode(TimeSpan time, double fps)
        {
             double totalSeconds = time.TotalSeconds;
             int h = (int)totalSeconds / 3600;
             int m = ((int)totalSeconds % 3600) / 60;
             int s = (int)totalSeconds % 60;
             int f = (int)Math.Round((totalSeconds - (int)totalSeconds) * fps);
             if (f >= fps) f = 0;
             return $"{h:D2}:{m:D2}:{s:D2}:{f:D2}";
        }
    }

    // Inner Models
    public partial class MediaFile : ObservableObject
    {
        [ObservableProperty] public string _filename = "";
        [ObservableProperty] public string _fullPath = "";
        [ObservableProperty] public string _startTimecode = "";
        [ObservableProperty] public string _duration = "";
        [ObservableProperty] public double _fps = 25;
        
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
    }

    public partial class SyncPair : ObservableObject
    {
        [ObservableProperty] public MediaFile _video = new();
        [ObservableProperty] public MediaFile? _audio; // Can be null if no match
        [ObservableProperty] public string _status = "Pending";
        [ObservableProperty] public string _offsetDisplay = "";
        [ObservableProperty] public SyncMethod _syncMethod = SyncMethod.Manual;
        
        public double OffsetSeconds { get; set; }
        public string SyncMethodDisplay => SyncMethod switch
        {
            SyncMethod.Timecode => "🕐 Timecode",
            SyncMethod.Waveform => "🎵 Waveform",
            SyncMethod.Manual => "✋ Manual",
            _ => "Unknown"
        };
    }
}
