using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Threading;
using Veriflow.Avalonia.Services;
using Veriflow.Avalonia.Models;
using LibVLCSharp.Shared;

namespace Veriflow.Avalonia.ViewModels
{
    public partial class AudioPlayerViewModel : ObservableObject, IDisposable
    {
        // TODO: Replace with Cross-Platform Audio Engine (e.g. Bass or PortAudio)
        // private ISoundOut? _outputDevice;
        // private IWaveSource? _inputStream;
        // private MultiChannelAudioMixer? _mixer;
        // private VeriflowMeteringProvider? _meteringProvider;
        
        public bool ShowVolumeControls => false; // Audio Player uses external mixer console

        // LibVLC for video playback
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;

        [ObservableProperty]
        private MediaPlayer? _mediaPlayerInstance;

        private readonly DispatcherTimer _playbackTimer;
        private double _fps = 25.0; // Default Frame Rate
        private System.Diagnostics.Stopwatch _stopwatch = new();
        // private TimeSpan _lastMediaTime; // Unused - commented out

        [ObservableProperty]
        private string _filePath = "No file loaded";

        [ObservableProperty]
        private string _fileName = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        [NotifyCanExecuteChangedFor(nameof(SendToOffloadCommand))]
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
                Interval = TimeSpan.FromMilliseconds(16) // 60fps
            };
            _playbackTimer.Tick += OnTimerTick;
            
            // Initialize LibVLC for video playback
            try
            {
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);
                MediaPlayerInstance = _mediaPlayer;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LibVLC initialization error: {ex.Message}");
            }
        }

        // private bool _isTimerUpdating; // Unused - commented out

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
            // Stub simulation
            // if (_inputStream != null) ...
            
            // Simulation for UI testing
            if (IsPlaying)
            {
                PlaybackPosition += 0.016;
                if (PlaybackPosition > PlaybackMaximum) PlaybackPosition = 0;
                CurrentTimeDisplay = FormatTimecode(TimeSpan.FromSeconds(PlaybackPosition));
                PlaybackPercent = PlaybackPosition / (PlaybackMaximum > 0 ? PlaybackMaximum : 1);
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
            // if (_inputStream != null && !_isTimerUpdating) ...
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

        // LoadFile should always be available
        [RelayCommand]
        private async Task LoadFile()
        {
            // Request file picker from view
            RequestFilePicker?.Invoke();
            await Task.CompletedTask;
        }

        // Event for view to handle file picking
        public event Action? RequestFilePicker;

        public async Task LoadAudio(string path)
        {
            try
            {
                await Stop();
                CleanUpAudio();

                // STUB: Valid Audio Engine Code Removed
                // _inputStream = CodecFactory.Instance.GetCodec(path); ...

                FilePath = path;
                FileName = System.IO.Path.GetFileName(path);
                IsAudioLoaded = true;

                // Fake duration
                PlaybackMaximum = 60.0;
                TotalTimeDisplay = "00:01:00:00";

                await LoadMetadataWithFFprobe(path);

                // Track in recent files
                Services.RecentFilesService.Instance.AddRecentFile(path);
            
                // Update sibling files
                UpdateSiblingFiles(path);
                
                // Parse Frame Rate
                if (!string.IsNullOrEmpty(CurrentMetadata.FrameRate))
                {
                    string normalizedFn = CurrentMetadata.FrameRate.Replace(',', '.');
                    string fpsString = new string(normalizedFn.Where(c => char.IsDigit(c) || c == '.').ToArray());
                    
                    if (double.TryParse(fpsString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedFps))
                    {
                        if (parsedFps > 0 && parsedFps < 120) 
                        {
                            _fps = parsedFps;
                        }
                    }
                }

                InitializeTracks(CurrentMetadata.ChannelCount > 0 ? CurrentMetadata.ChannelCount : 2);
                
                GenerateWaveforms(path);
                GenerateRuler();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading file: {ex.Message}");
            }
        }

        [ObservableProperty]
        private ObservableCollection<RulerTick> _rulerTicks = new();

        private void GenerateRuler()
        {
            RulerTicks.Clear();
            
            double totalSeconds = PlaybackMaximum;
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
        private void Seek(PointerPressedEventArgs e)
        {
            if (e.Source is global::Avalonia.Controls.Control element)
            {
                var position = e.GetPosition(element);
                var width = element.Bounds.Width;

                if (width > 0)
                {
                    double percent = position.X / width;
                    double seekTime = percent * PlaybackMaximum;
                    
                    if (seekTime < 0) seekTime = 0;
                    if (seekTime > PlaybackMaximum) seekTime = PlaybackMaximum;

                    // _inputStream.SetPosition...
                    PlaybackPosition = seekTime;
                }
            }
        }

        private void OnTrackVolumeChanged(int channel, float volume)
        {
            // _mixer?.SetChannelVolume(channel, volume);
        }

        private void OnTrackPanChanged(int channel, float pan)
        {
            // _mixer?.SetChannelPan(channel, pan);
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
             // Stub
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
            // Stub waveform generation
            // Requires Audio Decode
             Dispatcher.UIThread.Invoke(() =>
            {
                 foreach(var t in Tracks)
                 {
                     t.WaveformPoints = new AvaloniaList<Point>();
                 }
            });
        }

        private void OnPlaybackStopped(object? sender, EventArgs e)
        {
            IsPlaying = false;
            _playbackTimer.Stop();
        }


        [RelayCommand(CanExecute = nameof(CanPlay))]
        private void Play()
        {
             // Stub
             IsPlaying = true;
             IsPaused = false;
             _playbackTimer.Start();
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
            IsPlaying = false;
            IsPaused = false;
            PlaybackPosition = 0;
            
            await Task.Delay(200);
            IsStopPressed = false;
        }

        [RelayCommand(CanExecute = nameof(CanPause))]
        private void Pause()
        {
             IsPlaying = false;
             IsPaused = true;
             _playbackTimer.Stop();
        }

        private bool CanPause() => IsAudioLoaded && IsPlaying;
        private bool CanPlay() => IsAudioLoaded && !IsPlaying;
        private bool CanStop() => IsAudioLoaded;

        private void CleanUpAudio()
        {
             // Stub
        }

        public void Dispose()
        {
            _playbackTimer?.Stop();
             if (_playbackTimer != null)
                 _playbackTimer.Tick -= OnTimerTick;
            
             CleanUpAudio();
             GC.SuppressFinalize(this);
        }
        public event Action<IEnumerable<string>>? RequestTranscode;
        public event Action<string>? RequestModifyReport;
        public event Action<string>? RequestOffloadSource;

        private bool CanSendToOffload() => IsAudioLoaded && !string.IsNullOrEmpty(FilePath);

        [RelayCommand(CanExecute = nameof(CanSendToOffload))]
        private void SendToOffload()
        {
            if (CanSendToOffload())
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

        [RelayCommand(CanExecute = nameof(CanSendFileToTranscode))] 
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

        // --- FRAME NAVIGATION COMMANDS ---

        [RelayCommand]
        private void PreviousFrame()
        {
            if (IsAudioLoaded)
            {
                double secondsPerFrame = 1.0 / _fps;
                PlaybackPosition = Math.Max(0, PlaybackPosition - secondsPerFrame);
                CurrentTimeDisplay = FormatTimecode(TimeSpan.FromSeconds(PlaybackPosition));
            }
        }

        [RelayCommand]
        private void NextFrame()
        {
            if (IsAudioLoaded)
            {
                double secondsPerFrame = 1.0 / _fps;
                PlaybackPosition = Math.Min(PlaybackMaximum, PlaybackPosition + secondsPerFrame);
                CurrentTimeDisplay = FormatTimecode(TimeSpan.FromSeconds(PlaybackPosition));
            }
        }

        [RelayCommand]
        private void Rewind()
        {
            if (IsAudioLoaded)
            {
                PlaybackPosition = Math.Max(0, PlaybackPosition - 1.0);
                CurrentTimeDisplay = FormatTimecode(TimeSpan.FromSeconds(PlaybackPosition));
            }
        }

        [RelayCommand]
        private void Forward()
        {
            if (IsAudioLoaded)
            {
                PlaybackPosition = Math.Min(PlaybackMaximum, PlaybackPosition + 1.0);
                CurrentTimeDisplay = FormatTimecode(TimeSpan.FromSeconds(PlaybackPosition));
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
            
            NavigatePreviousCommand.NotifyCanExecuteChanged();
            NavigateNextCommand.NotifyCanExecuteChanged();
        }
    }
}
