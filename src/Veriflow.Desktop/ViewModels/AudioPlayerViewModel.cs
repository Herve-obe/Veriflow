using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using CSCore;
using CSCore.Codecs;
using CSCore.SoundOut;
using CSCore.Streams;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Veriflow.Desktop.Services;
using Veriflow.Desktop.Models;

namespace Veriflow.Desktop.ViewModels
{
    public partial class AudioPlayerViewModel : ObservableObject, IDisposable
    {
        private ISoundOut? _outputDevice;
        private IWaveSource? _inputStream;
        private MultiChannelAudioMixer? _mixer;
        private VeriflowMeteringProvider? _meteringProvider;
        
        public bool ShowVolumeControls => false; // Audio Player uses external mixer console

        private readonly DispatcherTimer _playbackTimer;
        private double _fps = 25.0; // Default Frame Rate
        private System.Diagnostics.Stopwatch _stopwatch = new();
        private TimeSpan _lastMediaTime;

        [ObservableProperty]
        private string _filePath = "No file loaded";

        [ObservableProperty]
        private string _fileName = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        [NotifyCanExecuteChangedFor(nameof(SendFileToTranscodeCommand))]
        [NotifyCanExecuteChangedFor(nameof(UnloadMediaCommand))]
        [NotifyPropertyChangedFor(nameof(HasMedia))]
        private bool _isAudioLoaded;

        public bool HasMedia => IsAudioLoaded;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        private bool _isPlaying;

        [ObservableProperty]
        private string _currentTimeDisplay = "00:00:00:00";

        [ObservableProperty]
        private string _totalTimeDisplay = "00:00:00:00";

        [ObservableProperty]
        private double _playbackPosition;

        [ObservableProperty]
        private double _playbackMaximum = 1;

        [ObservableProperty]
        private double _playbackPercent = 0;

        [ObservableProperty]
        private AudioMetadata _currentMetadata = new();

        public ObservableCollection<TrackViewModel> Tracks { get; } = new();

        public AudioPlayerViewModel()
        {
            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // 60fps - essential for ultra-smooth timecode/frame display
            };
            _playbackTimer.Tick += OnTimerTick;
        }

        private bool _isTimerUpdating;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        private bool _isPaused;

        // --- FILE NAVIGATION ---
        private readonly FileNavigationService _fileNavigationService = new();
        private static readonly string[] AudioExtensions = { ".wav", ".mp3", ".aif", ".aiff", ".flac", ".m4a", ".aac", ".ogg" };
        private List<string> _siblingFiles = new();
        private int _currentFileIndex = -1;

        [ObservableProperty]
        private bool _isStopPressed;

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_inputStream != null)
            {
                var time = _inputStream.GetPosition();
                
                 // Interpolation Logic
                if (time != _lastMediaTime)
                {
                    _lastMediaTime = time;
                    _stopwatch.Restart();
                }
                
                // Interpolated time
                var interpolatedTime = _lastMediaTime + _stopwatch.Elapsed;
                if (interpolatedTime > _inputStream.GetLength()) interpolatedTime = _inputStream.GetLength();
                
                CurrentTimeDisplay = FormatTimecode(interpolatedTime);
                
                _isTimerUpdating = true;
                PlaybackPosition = interpolatedTime.TotalSeconds;
                if (PlaybackMaximum > 0)
                    PlaybackPercent = PlaybackPosition / PlaybackMaximum;
                
                if (_meteringProvider != null)
                {
                   var peaks = _meteringProvider.ChannelPeaks;
                   // Tracks count check to prevent index out of range
                   for(int i=0; i < Tracks.Count && i < peaks.Length; i++)
                   {
                       float linear = peaks[i];
                       if (linear < 0) linear = 0;
                       float db = (linear > 0) ? 20 * (float)Math.Log10(linear) : -60;
                       if (db < -60) db = -60;
                       float normalized = (db + 60) / 60f;
                       if (normalized < 0) normalized = 0;
                       if (normalized > 1) normalized = 1;
                       Tracks[i].CurrentLevel = normalized;
                   }
                }

                _isTimerUpdating = false;
            }
        }

        private string FormatTimecode(TimeSpan time)
        {
            double offsetSeconds = CurrentMetadata?.TimeReferenceSeconds ?? 0;
            var offset = TimeSpan.FromSeconds(offsetSeconds);
            return Services.TimecodeHelper.FormatTimecode(time, _fps, offset);
        }

        partial void OnPlaybackPositionChanged(double value)
        {
                if (_inputStream != null && !_isTimerUpdating)
            {
                if (value >= 0 && value <= _playbackMaximum)
                {
                    _inputStream.SetPosition(TimeSpan.FromSeconds(value));
                    if (PlaybackMaximum > 0)
                        PlaybackPercent = value / PlaybackMaximum;
                }
            }
        }

        private bool CanUnloadMedia() => IsAudioLoaded;

        [RelayCommand(CanExecute = nameof(CanUnloadMedia))]
        private async Task UnloadMedia()
        {
            await Stop();
            CleanUpAudio();

            FilePath = "No file loaded";
            FileName = "";
            IsAudioLoaded = false;
            CurrentTimeDisplay = "00:00:00:00";
            TotalTimeDisplay = "00:00:00:00";
            PlaybackPosition = 0;
            PlaybackPercent = 0;
            
            Tracks.Clear();
            RulerTicks.Clear();
            CurrentMetadata = new AudioMetadata();

            _siblingFiles.Clear();
            _currentFileIndex = -1;
        }

        [RelayCommand]
        private async Task LoadFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                // Expanded extensions in Filter
                Filter = "Audio Files|*.wav;*.bwf;*.flac;*.mp3;*.aiff;*.m4a;*.ogg;*.wma|All Files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await LoadAudio(openFileDialog.FileName);
            }
        }

        public async Task LoadAudio(string path)
        {
            try
            {
                await Stop();
                CleanUpAudio();

                try
                {
                    _inputStream = CodecFactory.Instance.GetCodec(path);
                }
                catch (Exception ex)
                {
                     MessageBox.Show($"Error loading audio codec: {ex.Message}");
                     return;
                }

                var sampleSource = _inputStream.ToSampleSource();
                
                _meteringProvider = new VeriflowMeteringProvider(sampleSource);
                _mixer = new MultiChannelAudioMixer(_meteringProvider); 
                
                var finalSource = _mixer.ToWaveSource();

                // Revert to 200ms latency but keeping multi-channel mixer
                _outputDevice = new WasapiOut(true, CSCore.CoreAudioAPI.AudioClientShareMode.Shared, 200);
                _outputDevice.Initialize(finalSource);
                _outputDevice.Stopped += OnPlaybackStopped;

                FilePath = path;
                FileName = System.IO.Path.GetFileName(path);
                IsAudioLoaded = true;

                TotalTimeDisplay = FormatTimecode(_inputStream.GetLength());
                PlaybackMaximum = _inputStream.GetLength().TotalSeconds;

                await LoadMetadataWithFFprobe(path);

                // Track in recent files
                Services.RecentFilesService.Instance.AddRecentFile(path);
            
                // Update sibling files for navigation
                UpdateSiblingFiles(path);
                
                // Try to parse frame rate from metadata if available
                if (!string.IsNullOrEmpty(CurrentMetadata.FrameRate))
                {
                    // FrameRate format in AudioMetadata might be "25 ND" or "23.98"
                    string normalizedFn = CurrentMetadata.FrameRate.Replace(',', '.');
                    string fpsString = new string(normalizedFn.Where(c => char.IsDigit(c) || c == '.').ToArray());
                    
                    if (double.TryParse(fpsString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedFps))
                    {
                        // Sanity Check: If FPS > 120, it's likely a Sample Rate (e.g. 48000) or invalid. Ignore it.
                        if (parsedFps > 0 && parsedFps < 120) 
                        {
                            _fps = parsedFps;
                        }
                        // Refresh Total Display with new (or default) FPS
                        TotalTimeDisplay = FormatTimecode(_inputStream.GetLength());
                    }
                }

                InitializeTracks(CurrentMetadata.ChannelCount > 0 ? CurrentMetadata.ChannelCount : _inputStream.WaveFormat.Channels);
                
                GenerateWaveforms(path);
                GenerateRuler();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [ObservableProperty]
        private ObservableCollection<RulerTick> _rulerTicks = new();

        private void GenerateRuler()
        {
            RulerTicks.Clear();
            if (_inputStream == null) return;

            var totalSeconds = _inputStream.GetLength().TotalSeconds;
            if (totalSeconds <= 0) return;

            double absStart = CurrentMetadata?.TimeReferenceSeconds ?? 0;

            int tickCount = 10;
            double step = totalSeconds / tickCount;

            for (int i = 0; i <= tickCount; i++)
            {
                double time = i * step; 
                double absoluteTime = absStart + time;
                
                var tsRel = TimeSpan.FromSeconds(absoluteTime);
                
                RulerTicks.Add(new RulerTick
                {
                    Percent = (double)i / tickCount,
                    Label = tsRel.ToString(@"hh\:mm\:ss") 
                });
            }
        }

        [RelayCommand]
        private void Seek(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_inputStream == null) return;

            if (e.Source is FrameworkElement element)
            {
                var position = e.GetPosition(element);
                var width = element.ActualWidth;

                if (width > 0)
                {
                    double percent = position.X / width;
                    double seekTime = percent * _inputStream.GetLength().TotalSeconds;
                    
                    if (seekTime < 0) seekTime = 0;
                    if (seekTime > _inputStream.GetLength().TotalSeconds) seekTime = _inputStream.GetLength().TotalSeconds;

                    _inputStream.SetPosition(TimeSpan.FromSeconds(seekTime));
                    PlaybackPosition = seekTime;
                }
            }
        }

        private void OnTrackVolumeChanged(int channel, float volume)
        {
            _mixer?.SetChannelVolume(channel, volume);
        }

        private void OnTrackPanChanged(int channel, float pan)
        {
            _mixer?.SetChannelPan(channel, pan);
        }

        private void OnTrackMuteChanged(int channel, bool isMuted)
        {
            UpdateMixerState();
        }

        private void OnTrackSoloChanged(TrackViewModel track)
        {
            UpdateMixerState();
        }

        private void UpdateMixerState()
        {
            if (_mixer == null) return;

            foreach (var track in Tracks)
            {
                _mixer.SetChannelMute(track.ChannelIndex, track.IsMuted);
                _mixer.SetChannelSolo(track.ChannelIndex, track.IsSoloed);
            }
        }

        private void InitializeTracks(int channelCount)
        {
            Tracks.Clear();
            var iXmlNames = CurrentMetadata?.TrackNames ?? new List<string>();

            for (int i = 0; i < channelCount; i++)
            {
                string name = $"TRK {i + 1}";
                
                if (i < iXmlNames.Count && !string.IsNullOrWhiteSpace(iXmlNames[i]))
                {
                    name = iXmlNames[i];
                }
                
                var track = new TrackViewModel(i, name, 
                    OnTrackSoloChanged, 
                    OnTrackVolumeChanged, 
                    OnTrackMuteChanged, 
                    OnTrackPanChanged
                );
                Tracks.Add(track);
            }
        }

        private void GenerateWaveforms(string path)
        {
            Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await Task.Run(() =>
                {
                    try
                    {
                        using var codec = CodecFactory.Instance.GetCodec(path);
                        var source = codec.ToSampleSource();
                        int totalChannels = source.WaveFormat.Channels;
                        
                        int width = 500;
                        
                        long totalSamplesAllChannels = source.Length / 4; 
                        try { totalSamplesAllChannels = source.Length; } catch {} 

                        long samplesPerChannel = (source.Length / totalChannels); 
                        long samplesPerPoint = samplesPerChannel / width;

                        if (samplesPerPoint < 1) samplesPerPoint = 1;

                        var maxBuffers = new float[totalChannels][]; 
                        for(int c=0; c<totalChannels; c++) maxBuffers[c] = new float[width];

                        float[] buffer = new float[samplesPerPoint * totalChannels];
                        int pointIndex = 0;

                        while (pointIndex < width)
                        {
                            int read = source.Read(buffer, 0, buffer.Length);
                            if (read == 0) break;

                            for (int c = 0; c < totalChannels; c++)
                            {
                                float max = 0;
                                for (int i = c; i < read; i += totalChannels)
                                {
                                    float val = Math.Abs(buffer[i]);
                                    if (val > max) max = val;
                                }
                                maxBuffers[c][pointIndex] = max;
                            }
                            pointIndex++;
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            for (int c = 0; c < totalChannels && c < Tracks.Count; c++)
                            {
                                var trackPoints = new PointCollection();
                                float[] peaks = maxBuffers[c];
                                
                                for (int x = 0; x < width; x++)
                                {
                                    trackPoints.Add(new Point(x, 20 - (peaks[x] * 19))); 
                                }
                                
                                 for (int x = width - 1; x >= 0; x--)
                                {
                                    trackPoints.Add(new Point(x, 20 + (peaks[x] * 19))); 
                                }

                                Tracks[c].WaveformPoints = trackPoints;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Waveform Generation Error: {ex.Message}");
                    }
                });
            });
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            IsPlaying = false;
            _playbackTimer.Stop();
            
            if (!IsPaused)
            {
                PlaybackPosition = 0;
                CurrentTimeDisplay = "00:00:00:00";
                if (_inputStream != null) _inputStream.Position = 0;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var t in Tracks) t.CurrentLevel = 0;
                });
            }
        }


        [RelayCommand(CanExecute = nameof(CanPlay))]
        private void Play()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Play();
                _playbackTimer.Start();
                _stopwatch.Restart();
                IsPaused = false;
                IsPlaying = true;
                IsStopPressed = false;
            }
        }

        [RelayCommand]
        private void TogglePlayPause()
        {
            if (IsPlaying)
                Pause();
            else if (CanPlay())
                Play();
        }

        [RelayCommand(CanExecute = nameof(CanStop))]
        private async Task Stop()
        {
            IsStopPressed = true;

            _playbackTimer.Stop();
            _stopwatch.Reset();
            IsPaused = false;
            IsPlaying = false;
            PlaybackPosition = 0;
            PlaybackPercent = 0;
            CurrentTimeDisplay = "00:00:00:00";

            if (_inputStream != null)
            {
                _inputStream.Position = 0;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var t in Tracks) t.CurrentLevel = 0;
            });

            if (_outputDevice != null)
            {
                _outputDevice.Stop();
            }

            await Task.Delay(200);
            IsStopPressed = false;
        }

        [RelayCommand(CanExecute = nameof(CanPause))]
        private void Pause()
        {
            if (_outputDevice != null && _outputDevice.PlaybackState == PlaybackState.Playing)
            {
                _playbackTimer.Stop(); 
                _stopwatch.Stop();
                _outputDevice.Pause();
                IsPaused = true;
                IsPlaying = false;
            }
        }

        private bool CanPause() => IsAudioLoaded && _outputDevice?.PlaybackState == PlaybackState.Playing;
        private bool CanPlay() => IsAudioLoaded && !IsPlaying;
        private bool CanStop() => IsAudioLoaded;

        private void CleanUpAudio()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Dispose();
                _outputDevice = null;
            }
            if (_inputStream != null)
            {
                _inputStream.Dispose();
                _inputStream = null;
            }
            _mixer = null;
            _meteringProvider = null;
        }

        public void Dispose()
        {
            // Stop timer
            _playbackTimer?.Stop();
            _stopwatch?.Stop();
            
            // Unsubscribe from timer event
            if (_playbackTimer != null)
            {
                _playbackTimer.Tick -= OnTimerTick;
            }
            
            // Unsubscribe from output device event
            if (_outputDevice != null)
            {
                _outputDevice.Stopped -= OnPlaybackStopped;
            }
            
            // Clean up audio resources
            CleanUpAudio();
            
            // Suppress finalization
            GC.SuppressFinalize(this);
        }
        public event Action<IEnumerable<string>>? RequestTranscode;
        public event Action<string>? RequestModifyReport;
        public event Action<string>? RequestOffloadSource;

        private bool CanSendToSecureCopy() => IsAudioLoaded && !string.IsNullOrEmpty(FilePath);

        [RelayCommand(CanExecute = nameof(CanSendToSecureCopy))]
        private void SendToSecureCopy()
        {
            if (CanSendToSecureCopy())
            {
                string dir = System.IO.Path.GetDirectoryName(FilePath) ?? "";
                if (!string.IsNullOrEmpty(dir))
                    RequestOffloadSource?.Invoke(dir);
            }
        }
        
        private bool CanSendFileToTranscode() => IsAudioLoaded && !string.IsNullOrEmpty(FilePath);

        [RelayCommand(CanExecute = nameof(CanSendFileToTranscode))]
        private void SendFileToTranscode()
        {
            if (CanSendFileToTranscode())
            {
                RequestTranscode?.Invoke(new[] { FilePath });
            }
        }

        [RelayCommand(CanExecute = nameof(CanSendFileToTranscode))] // Reusing condition as it checks file loaded + path
        private void ModifyInReport()
        {
             if (!string.IsNullOrEmpty(FilePath))
             {
                 RequestModifyReport?.Invoke(FilePath);
             }
        }
        private async Task LoadMetadataWithFFprobe(string path)
        {
            try
            {
                var metaProvider = new FFprobeMetadataProvider();
                CurrentMetadata = await metaProvider.GetMetadataAsync(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Metadata Error: {ex.Message}");
                CurrentMetadata = new AudioMetadata { Filename = System.IO.Path.GetFileName(path) };
            }
        }

        // --- FRAME NAVIGATION COMMANDS (Same as Video Player) ---

        [RelayCommand]
        private void PreviousFrame()
        {
            if (_inputStream != null && IsAudioLoaded)
            {
                // Calculate Frame Duration in seconds (1 / FPS)
                double secondsPerFrame = 1.0 / _fps;
                
                var currentTime = _inputStream.GetPosition();
                var targetTime = currentTime.Subtract(TimeSpan.FromSeconds(secondsPerFrame));
                
                // Clamp to 0
                if (targetTime < TimeSpan.Zero) targetTime = TimeSpan.Zero;
                
                _inputStream.SetPosition(targetTime);
                PlaybackPosition = targetTime.TotalSeconds;
                CurrentTimeDisplay = Services.TimecodeHelper.FormatTimecode(targetTime, _fps);
            }
        }

        [RelayCommand]
        private void NextFrame()
        {
            if (_inputStream != null && IsAudioLoaded)
            {
                // Calculate Frame Duration in seconds (1 / FPS)
                double secondsPerFrame = 1.0 / _fps;
                
                var currentTime = _inputStream.GetPosition();
                var targetTime = currentTime.Add(TimeSpan.FromSeconds(secondsPerFrame));
                
                // Clamp to length
                var length = _inputStream.GetLength();
                if (targetTime > length) targetTime = length;
                
                _inputStream.SetPosition(targetTime);
                PlaybackPosition = targetTime.TotalSeconds;
                CurrentTimeDisplay = Services.TimecodeHelper.FormatTimecode(targetTime, _fps);
            }
        }

        [RelayCommand]
        private void Rewind()
        {
            if (_inputStream != null && IsAudioLoaded)
            {
                var currentTime = _inputStream.GetPosition();
                var targetTime = currentTime.Subtract(TimeSpan.FromSeconds(1)); // 1 second jump
                
                // Clamp to 0
                if (targetTime < TimeSpan.Zero) targetTime = TimeSpan.Zero;
                
                _inputStream.SetPosition(targetTime);
                PlaybackPosition = targetTime.TotalSeconds;
                CurrentTimeDisplay = Services.TimecodeHelper.FormatTimecode(targetTime, _fps);
            }
        }

        [RelayCommand]
        private void Forward()
        {
            if (_inputStream != null && IsAudioLoaded)
            {
                var currentTime = _inputStream.GetPosition();
                var targetTime = currentTime.Add(TimeSpan.FromSeconds(1)); // 1 second jump
                
                // Clamp to length
                var length = _inputStream.GetLength();
                if (targetTime > length) targetTime = length;
                
                _inputStream.SetPosition(targetTime);
                PlaybackPosition = targetTime.TotalSeconds;
                CurrentTimeDisplay = Services.TimecodeHelper.FormatTimecode(targetTime, _fps);
            }
        }

        // --- FILE NAVIGATION COMMANDS ---

        [RelayCommand(CanExecute = nameof(CanNavigatePrevious))]
        private async Task NavigatePrevious()
        {
            if (_currentFileIndex > 0)
            {
                await LoadAudio(_siblingFiles[_currentFileIndex - 1]);
            }
        }

        private bool CanNavigatePrevious() => _currentFileIndex > 0;

        [RelayCommand(CanExecute = nameof(CanNavigateNext))]
        private async Task NavigateNext()
        {
            if (_currentFileIndex < _siblingFiles.Count - 1)
            {
                await LoadAudio(_siblingFiles[_currentFileIndex + 1]);
            }
        }

        private bool CanNavigateNext() => _currentFileIndex >= 0 && _currentFileIndex < _siblingFiles.Count - 1;

        private void UpdateSiblingFiles(string currentPath)
        {
            (_siblingFiles, _currentFileIndex) = _fileNavigationService.GetSiblingFiles(currentPath, AudioExtensions);
            
            // Update command states
            NavigatePreviousCommand.NotifyCanExecuteChanged();
            NavigateNextCommand.NotifyCanExecuteChanged();
        }
    }
}
