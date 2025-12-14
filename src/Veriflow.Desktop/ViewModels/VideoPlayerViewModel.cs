using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using Veriflow.Desktop.Models;
using Veriflow.Desktop.Services;
using System.Linq;

namespace Veriflow.Desktop.ViewModels
{
    public partial class VideoPlayerViewModel : ObservableObject, IDisposable
    {
        private MediaPlayer? _mediaPlayer;

        public bool ShowVolumeControls => true; // Video Player uses internal volume slider

        // Unified Volume Control
        private float _volume = 1.0f;
        private float _preMuteVolume = 1.0f; // Store volume before muting

        private double _fps = 25.0; // Default Frame Rate
        private long _lastMediaTime;
        private System.Diagnostics.Stopwatch _stopwatch = new();
        private TimeSpan _startHeaderOffset = TimeSpan.Zero;

        public float Volume
        {
            get => _volume;
            set
            {
                if (SetProperty(ref _volume, value))
                {
                    if (_mediaPlayer != null) _mediaPlayer.Volume = (int)(value * 100);

                    // "Unmute on Drag" feature
                    // Fix "Ping-Pong": Only unmute if currently muted.
                    // IMPORTANT: The UI slider sets this. If user drags slider up, value > 0.
                    // We simply set IsMuted = false. 
                    // The IsMuted setter will handle the rest (and NOT restore old volume because Volume > 0).
                    if (value > 0 && IsMuted)
                    {
                        IsMuted = false;
                    }
                }
            }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (SetProperty(ref _isMuted, value))
                {
                    if (_mediaPlayer != null) _mediaPlayer.Mute = value;

                    if (value) // MUTE ACTIVATION
                    {
                        // Store current volume if valid
                        if (Volume > 0)
                        {
                            _preMuteVolume = Volume;
                        }
                        // Drop UI to 0
                        Volume = 0;
                    }
                    else // MUTE DEACTIVATION
                    {
                        // CRITICAL FIX for Ping-Pong Effect:
                        // Only restore _preMuteVolume if the current Volume is 0.
                        // If Volume > 0, it means the user UNMUTED by DRAGGING the slider (Volume property set first).
                        // In that case, we respect the user's dragged value and DO NOT overwrite it.
                        if (Volume == 0)
                        {
                            Volume = _preMuteVolume > 0.05f ? _preMuteVolume : 0.5f; // Default if too low
                        }
                    }
                }
            }
        }

        [RelayCommand]
        private void ToggleMute()
        {
            IsMuted = !IsMuted;
        }

        [ObservableProperty]
        private MediaPlayer? _player; // Expose to View for Binding

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMedia))]
        private string _filePath = "No file loaded";

        public bool HasMedia => !string.IsNullOrEmpty(FilePath) && FilePath != "No file loaded";

        [ObservableProperty]
        private string _fileName = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        [NotifyCanExecuteChangedFor(nameof(TogglePlayPauseCommand))]
        [NotifyCanExecuteChangedFor(nameof(SendFileToTranscodeCommand))]
        [NotifyCanExecuteChangedFor(nameof(UnloadMediaCommand))]
        private bool _isVideoLoaded;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(TogglePlayPauseCommand))]
        private bool _isPlaying;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(TogglePlayPauseCommand))]
        private bool _isPaused;

        [ObservableProperty]
        private string _currentTimeDisplay = "00:00:00:00";

        [ObservableProperty]
        private string _totalTimeDisplay = "00:00:00:00";

        [ObservableProperty]
        private double _playbackPercent;

        [ObservableProperty]
        private VideoMetadata _currentVideoMetadata = new();

        public event Action<IEnumerable<string>>? RequestTranscode;

        
        // --- REPORT NOTE EDITING ---
        private ReportItem? _linkedReportItem;

        [ObservableProperty]
        private string? _currentNote;

        [ObservableProperty]

        [NotifyCanExecuteChangedFor(nameof(UpdateReportCommand))]
        private bool _canEditNote;

        // Callback to find if current file is in Report
        public Func<string, ReportItem?>? GetReportItemCallback;


        // Timer for updating UI slider/time
        private readonly DispatcherTimer _uiTimer;
        private bool _isUserSeeking; // Prevent timer updates while dragging slider

        public VideoPlayerViewModel()
        {
            if (!DesignMode.IsDesignMode)
            {
                // USE SHARED ENGINE
                var libVLC = VideoEngineService.Instance.LibVLC;
                if (libVLC != null)
                {
                    _mediaPlayer = new MediaPlayer(libVLC);
                    Player = _mediaPlayer;

                    _mediaPlayer.LengthChanged += OnLengthChanged;
                    _mediaPlayer.EndReached += OnEndReached;
                    _mediaPlayer.EncounteredError += OnError;
                }
            }

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) }; // 100fps update for maximum fluidity
            _uiTimer.Tick += OnUiTick;
        }

        private void OnUiTick(object? sender, EventArgs e)
        {
            if (_mediaPlayer != null && _mediaPlayer.IsPlaying && !_isUserSeeking)
            {
                var time = _mediaPlayer.Time;
                
                // Interpolation Logic
                if (time != _lastMediaTime)
                {
                    _lastMediaTime = time;
                    _stopwatch.Restart();
                }

                // Interpolated time = valid media time + elapsed since last update
                var interpolatedTime = TimeSpan.FromMilliseconds(_lastMediaTime + _stopwatch.ElapsedMilliseconds);
                
                // Clamp to Length
                if (interpolatedTime.TotalMilliseconds > _mediaPlayer.Length)
                    interpolatedTime = TimeSpan.FromMilliseconds(_mediaPlayer.Length);

                var length = _mediaPlayer.Length;

                if (length > 0)
                {
                    PlaybackPercent = interpolatedTime.TotalMilliseconds / length;
                }
                
                CurrentTimeDisplay = FormatTimecode(interpolatedTime);
            }
        }

        private string FormatTimecode(TimeSpan time)
        {
            // Add Start Offset (Time Reference)
            var absoluteTime = time + _startHeaderOffset;

            // Format: hh:mm:ss:ii (frames)
            double totalSeconds = absoluteTime.TotalSeconds;
            int h = (int)absoluteTime.TotalHours; // Support hours > 24 if needed? Usually TC wraps or just goes up.
            int m = absoluteTime.Minutes;
            int s = absoluteTime.Seconds;
            int frames = (int)Math.Round((totalSeconds - (int)totalSeconds) * _fps);
            
            if (frames >= _fps) frames = 0; // Safety wrap

            return $"{h:D2}:{m:D2}:{s:D2}:{frames:D2}";
        }

        private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TotalTimeDisplay = FormatTimecode(TimeSpan.FromMilliseconds(e.Length));
            });
        }

        private void OnEndReached(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = false; // Updates UI button state
                IsPaused = false;
                _uiTimer.Stop();
                PlaybackPercent = 0;
                CurrentTimeDisplay = "00:00:00:00";
                
                // Reset player for replay
                _mediaPlayer?.Stop(); 
            });
        }

        private void OnError(object? sender, EventArgs e)
        {
             // Log error
             IsPlaying = false;
             _uiTimer.Stop();
        }




        public async Task LoadVideo(string path)
        {
             // Set Path first so Refresh can use it
             FilePath = path;
             RefreshReportLink();            
             
             await LoadMediaContext(path);
        }

        public void RefreshReportLink()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            ReportItem? linkedItem = null;
            if (GetReportItemCallback != null)
            {
                linkedItem = GetReportItemCallback.Invoke(FilePath);
            }

            _linkedReportItem = linkedItem;

            if (_linkedReportItem is ReportItem)
            {
                CanEditNote = true;
            }
            else
            {
                CanEditNote = false;
            }
            
            // Notify command
            UpdateReportCommand.NotifyCanExecuteChanged();
        }

        public async Task LoadMediaContext(string path)
        {
            try
            {
                await Stop(); // Stop any existing playback

                FilePath = path;
                FileName = System.IO.Path.GetFileName(path);

                var libVLC = VideoEngineService.Instance.LibVLC;

                if (libVLC != null && _mediaPlayer != null)
                {
                    var media = new Media(libVLC, path, FromType.FromPath);
                    await media.Parse(MediaParseOptions.ParseLocal);
                    _mediaPlayer.Media = media;
                    
                    // OPTIMIZATION: Set Loaded TRUE immediately so UI shows player
                    IsVideoLoaded = true;

                    // Trigger initial duration update if possible
                    if (media.Duration > 0)
                    {
                         TotalTimeDisplay = FormatTimecode(TimeSpan.FromMilliseconds(media.Duration));
                    }

                    // Load heavy metadata in background
                    // The UI is already active
                    await LoadMetadataWithFFprobe(path);
                    
                    // Initial Volume Set (Force update)
                    _mediaPlayer.Volume = (int)(Volume * 100);
                    _mediaPlayer.Mute = IsMuted;
                }
            }
            catch (Exception ex)
            {
                // Handle error
                System.Diagnostics.Debug.WriteLine($"Error loading video: {ex.Message}");
            }
        }

        private async Task LoadMetadataWithFFprobe(string path)
        {
             var provider = new FFprobeMetadataProvider();
             CurrentVideoMetadata = await provider.GetVideoMetadataAsync(path);
             
             // Parse FPS
             if (!string.IsNullOrEmpty(CurrentVideoMetadata.FrameRate))
             {
                 try
                 {
                     // Extract number from string like "24 fps" or "23.98 fps" or "25,00 fps"
                     string normalizedFn = CurrentVideoMetadata.FrameRate.Replace(',', '.');
                     string fpsString = new string(normalizedFn.Where(c => char.IsDigit(c) || c == '.').ToArray());
                     
                     if (double.TryParse(fpsString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedFps))
                     {
                         if (parsedFps > 0) _fps = parsedFps;
                     }
                 }
                 catch { /* Ignore parse error, keep default */ }
             }

             // Parse Start Timecode Offset
             _startHeaderOffset = TimeSpan.Zero;
             if (!string.IsNullOrEmpty(CurrentVideoMetadata.StartTimecode))
             {
                 try
                 {
                     // Expected format: HH:mm:ss:ff or HH:mm:ss
                     var parts = CurrentVideoMetadata.StartTimecode.Split(':');
                     if (parts.Length >= 3)
                     {
                         int h = int.Parse(parts[0]);
                         int m = int.Parse(parts[1]);
                         int s = int.Parse(parts[2]);
                         // Ignore frames for offset base, or converting frames to ms? 
                         // Usually TimeReference is seconds. But here we have string TC.
                         // Let's rely on TimeSpan parsing if possible or manual.
                         // Simplified: just Hours/Min/Sec for base offset. Frames might be trickier without knowing exact drop-frame logic?
                         // Let's try to keep it simple: Start TC is usually a fixed offset.
                         // If parts[3] exists (frames), convert to MS.
                         double ms = 0;
                         if (parts.Length == 4)
                         {
                             int f = int.Parse(parts[3]);
                             ms = (f / _fps) * 1000;
                         }
                         _startHeaderOffset = new TimeSpan(0, h, m, s, (int)ms);
                     }
                 }
                 catch { /* invalid TC string */ }
             }
        }

        [RelayCommand(CanExecute = nameof(CanPlay))]
        private void Play()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Play();
                _mediaPlayer.Mute = IsMuted;
                _mediaPlayer.Volume = (int)(Volume * 100);
                _uiTimer.Start();
                _stopwatch.Restart(); 
                IsPlaying = true;
                IsPaused = false;
            }
        }

        [RelayCommand]
        private void Pause()
        {
            if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Pause();
                _uiTimer.Stop();
                _stopwatch.Stop();
                IsPaused = true;
                IsPlaying = false;
            }
        }

        [RelayCommand]
        private void TogglePlayPause()
        {
            if (IsPlaying) Pause();
            else Play();
        }

        [ObservableProperty]
        private bool _isStopPressed;

        [RelayCommand(CanExecute = nameof(CanStop))]
        private async Task Stop()
        {
            IsStopPressed = true;

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
            }
            
            _uiTimer.Stop();
            _stopwatch.Reset();
            IsPlaying = false;
            IsPaused = false;
            PlaybackPercent = 0;
            PlaybackPercent = 0;
            CurrentTimeDisplay = "00:00:00:00";
            
            await Task.Delay(200);
            IsStopPressed = false;
        }

        [RelayCommand]
        private void Rewind()
        {
            if (_mediaPlayer != null && IsVideoLoaded)
            {
                var time = _mediaPlayer.Time - 5000;
                if (time < 0) time = 0;
                _mediaPlayer.Time = time;
                CurrentTimeDisplay = FormatTimecode(TimeSpan.FromMilliseconds(time));
            }
        }

        [RelayCommand]
        private void Forward()
        {
            if (_mediaPlayer != null && IsVideoLoaded)
            {
                var time = _mediaPlayer.Time + 5000;
                if (time > _mediaPlayer.Length) time = _mediaPlayer.Length;
                _mediaPlayer.Time = time;
                CurrentTimeDisplay = FormatTimecode(TimeSpan.FromMilliseconds(time));
            }
        }

        partial void OnPlaybackPercentChanged(double value)
        {
            if (_isUserSeeking && _mediaPlayer != null && IsVideoLoaded)
            {
                var length = _mediaPlayer.Length;
                if (length > 0)
                {
                    var seekTime = (long)(value * length);
                    if (Math.Abs(_mediaPlayer.Time - seekTime) > 500) // Debounce slightly
                    {
                         _mediaPlayer.Time = seekTime;
                         CurrentTimeDisplay = FormatTimecode(TimeSpan.FromMilliseconds(seekTime));
                    }
                }
            }
        }

        // Methods for slider interaction events (bound via Behaviours or Commands + Args)
        public void BeginSeek() => _isUserSeeking = true;
        public void EndSeek() => _isUserSeeking = false;

        private bool CanPlay() => IsVideoLoaded && !IsPlaying;
        private bool CanStop() => IsVideoLoaded;

        private bool CanUnloadMedia() => IsVideoLoaded;

        [RelayCommand]
        private async Task OpenFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.mov;*.mxf;*.avi;*.mkv;*.mts|All Files|*.*",
                Title = "Open Video File"
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadVideo(dialog.FileName);
            }
        }

        [RelayCommand]
        private async Task DropFile(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    string file = files[0];
                    string ext = System.IO.Path.GetExtension(file).ToLower();
                    // Valid video extensions (Expanded)
                    if (new[] { ".mp4", ".mov", ".mxf", ".avi", ".mkv", ".mts", ".ts", ".webm", ".flv", ".wmv", ".m4v" }.Contains(ext))
                    {
                        await LoadVideo(file);
                    }
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanUnloadMedia))]
        private void UnloadMedia()
        {
             if (_mediaPlayer != null)
             {
                 _mediaPlayer.Stop();
                 _mediaPlayer.Media?.Dispose();
                 _mediaPlayer.Media = null;
             }
             
             IsVideoLoaded = false;
             FileName = "";
             FilePath = "";
             CurrentTimeDisplay = FormatTimecode(TimeSpan.Zero);
             TotalTimeDisplay = FormatTimecode(TimeSpan.Zero);
             PlaybackPercent = 0;
             CurrentVideoMetadata = new VideoMetadata(); // Clear metadata
        }

        private bool CanSendFileToTranscode() => IsVideoLoaded && !string.IsNullOrEmpty(FilePath);

        [RelayCommand(CanExecute = nameof(CanSendFileToTranscode))]
        private void SendFileToTranscode()
        {
            if (CanSendFileToTranscode())
            {
                RequestTranscode?.Invoke(new[] { FilePath });
            }
        }

        [RelayCommand(CanExecute = nameof(CanEditNote))]
        private void UpdateReport()
        {
             if (_linkedReportItem is ReportItem item)
             {
                 var window = new Veriflow.Desktop.Views.ReportNoteWindow(item);
                 // Center Owner Logic is handled by WindowStartupLocation="CenterOwner" usually,
                 // but we need to ensure Owner is set.
                 if (Application.Current.MainWindow != null)
                 {
                     window.Owner = Application.Current.MainWindow;
                 }
                 
                 window.ShowDialog();
                 // No need to copy back values, DateBinding does it.
             }
        }



        public void Dispose()
        {
            _uiTimer.Stop();
            _mediaPlayer?.Dispose();
            // _libVLC should NOT be disposed here as it is shared
        }
    }

    public static class DesignMode
    {
        public static bool IsDesignMode 
        {
            get 
            { 
                return System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject());
            }
        }
    }
}

