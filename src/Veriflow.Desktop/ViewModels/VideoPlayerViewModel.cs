using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using Veriflow.Desktop.Models;
using Veriflow.Desktop.Services;
using System.Linq;
using System.IO;
using System.Text;
using Veriflow.Desktop.Views.Shared;

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



        // --- PROFESSIONAL LOGGING ---
        public ObservableCollection<ClipLogItem> TaggedClips { get; } = new();

        private TimeSpan? _currentInPoint;
        private TimeSpan? _currentOutPoint;

        [ObservableProperty]
        private string _currentMarkInDisplay = "xx:xx:xx:xx";

        [ObservableProperty]
        private string _currentMarkOutDisplay = "yy:yy:yy:yy";



        [ObservableProperty]
        private bool _isLoggingPending;

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
        private bool _isFromTimer; // Distinguish timer updates from user inputs
        
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
                    _isFromTimer = true;
                    PlaybackPercent = interpolatedTime.TotalMilliseconds / length;
                    _isFromTimer = false;
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
                    // Use :start-paused to ensure we render frame 0 but don't auto-start
                    // This is robust against race conditions.
                    var media = new Media(libVLC, path, FromType.FromPath);
                    media.AddOption(":start-paused");
                    await media.Parse(MediaParseOptions.ParseLocal);
                    _mediaPlayer.Media = media;
                    
                    // OPTIMIZATION: Set Loaded TRUE immediately so UI shows player
                    IsVideoLoaded = true;

                    // Activate the engine (will respect :start-paused)
                    _mediaPlayer.Play();
                    
                    // Update State to reflect "Paused at Start"
                    IsPlaying = false;
                    IsPaused = true;

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
            // Safety Check: Ensure Checked Loaded
            if (_mediaPlayer != null && IsVideoLoaded)
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

        [RelayCommand(CanExecute = nameof(CanStop))] // Disable if not loaded
        private void Pause()
        {
            if (_mediaPlayer != null && _mediaPlayer.IsPlaying && IsVideoLoaded)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Pause();
                _uiTimer.Stop();
                _stopwatch.Stop();
                IsPaused = true;
                IsPlaying = false;
            }
        }



        [RelayCommand(CanExecute = nameof(CanStop))] // Verified: Checks IsVideoLoaded. works for Toggle logic.
        private void TogglePlayPause()
        {
            if (!IsVideoLoaded) return;
            
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
                // REDEFINED STOP: Pause and Seek to 0
                // This keeps the engine active so we can still see the image or seek
                if (_mediaPlayer.IsPlaying) _mediaPlayer.Pause();
                _mediaPlayer.Time = 0;
            }
            
            _uiTimer.Stop();
            _stopwatch.Reset();
            IsPlaying = false;
            // VISUAL FIX: "Stop" means "Reset to Start". 
            // Although engine is paused, we don't want the "Pause" button to look active/toggled.
            // Setting IsPaused = false ensures the UI looks stopped/ready.
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
            // SEEK LOGIC:
            // Only seek if the change comes from the User (Click or Drag), NOT from the Timer.
            // We ignore _isUserSeeking here to allow "Click to Seek" (where IsUserSeeking remains false).
            if (!_isFromTimer && _mediaPlayer != null && IsVideoLoaded)
            {
                var length = _mediaPlayer.Length;
                if (length > 0)
                {
                    var seekTime = (long)(value * length);
                    
                    // Direct Seek (Precision depends on codec/VLC, usually decent)
                    // Removing 500ms debounce allows frame-by-frame scrubbing and precise clicking.
                    _mediaPlayer.Time = seekTime;
                    
                    // Immediate UI update for responsiveness
                    CurrentTimeDisplay = FormatTimecode(TimeSpan.FromMilliseconds(seekTime));
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



        // --- FRAME ACCURATE NAVIGATION ---
        
        private DispatcherTimer? _jogTimer;
        private DispatcherTimer? _delayTimer;
        private int _frameStepDirection; // 0=None, 1=Fwd, -1=Back
        
        [RelayCommand]
        private void KeyDown(KeyEventArgs e)
        {
            // Ignore OS key repeat to maintain our own timer-based cadence
            if (e.IsRepeat)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                StartJog(1);
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                StartJog(-1);
                e.Handled = true;
            }
        }

        [RelayCommand]
        private void KeyUp(KeyEventArgs e)
        {
            if (e.Key == Key.Right || e.Key == Key.Left)
            {
                StopJog();
                e.Handled = true;
            }
        }

        private void StartJog(int direction)
        {
             _frameStepDirection = direction;
             
             // 1. Immediate step (Short Press)
             PerformFrameStep();

             // 2. Start Latency Timer (Wait for Hold)
             if (_delayTimer == null)
             {
                 _delayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                 _delayTimer.Tick += (s, args) => 
                 {
                     _delayTimer.Stop();
                     StartContinuousJog();
                 };
             }
             _delayTimer.Start();
        }

        private void StartContinuousJog()
        {
             if (_jogTimer == null)
             {
                 // Fast JOG Rate (e.g. 30ms = ~33fps speed)
                 _jogTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                 _jogTimer.Tick += (s, args) => PerformFrameStep();
             }
             _jogTimer.Start();
        }

        private void StopJog()
        {
            _delayTimer?.Stop();
            _jogTimer?.Stop();
            _frameStepDirection = 0;
        }

        private void PerformFrameStep()
        {
            if (_mediaPlayer != null && IsVideoLoaded && _frameStepDirection != 0)
            {
                // Calculate Frame Duration in MS (1000 / FPS)
                double msPerFrame = 1000.0 / _fps;
                long step = (long)Math.Round(msPerFrame);
                
                // Safety: Minimum step 1ms
                if (step < 1) step = 1;

                long targetTime = _mediaPlayer.Time + (step * _frameStepDirection);
                
                // Clamp
                if (targetTime < 0) targetTime = 0;
                if (targetTime > _mediaPlayer.Length) targetTime = _mediaPlayer.Length;

                _mediaPlayer.Time = targetTime;
                
                // Update UI immediately for responsiveness
                CurrentTimeDisplay = FormatTimecode(TimeSpan.FromMilliseconds(targetTime));
            }
        }

        // --- COMMAND WRAPPERS FOR DIRECT BINDING IF NEEDED ---
        [RelayCommand]
        private void NextFrame() => SingleFrameStep(1);

        [RelayCommand]
        private void PreviousFrame() => SingleFrameStep(-1);

        private void SingleFrameStep(int direction)
        {
            _frameStepDirection = direction;
            PerformFrameStep();
            _frameStepDirection = 0;
        }

        // --- LOGGING COMMANDS ---

        [RelayCommand]
        private void SetIn()
        {
            if (_mediaPlayer == null || !IsVideoLoaded) return;
            
            _currentInPoint = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
            CurrentMarkInDisplay = FormatTimecode(_currentInPoint.Value);
            IsLoggingPending = true;
        }

        [RelayCommand]
        private void SetOut()
        {
            if (_mediaPlayer == null || !IsVideoLoaded) return;
            
            _currentOutPoint = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
            CurrentMarkOutDisplay = FormatTimecode(_currentOutPoint.Value);
            IsLoggingPending = true;
        }

        [RelayCommand]
        private void TagClip()
        {
             // Validate Points
             if (_currentInPoint == null || _currentOutPoint == null)
             {
                 // Logic: If user tries to Tag without In/Out, we could either ignore or set defaults.
                 // The instruction implies "If both... are valid".
                 // However, for UX, if one is missing, we might want to auto-fill or fail. 
                 // Sticking to strict "Commit" logic: Only commit if valid.
                 // For safety/fallback (as in previous code), I will retain the fallback logic but only if points actally exist or explicitly allowing fallback if current setup implies immediate tag.
                 // Requirement: "The 'TAG Clip' command validates these temporary points and commits them"
                 
                 // Fallback: If null, grab current time? The prompt implies Strict 2-step.
                 // But previous implementation allowed immediate tag.
                 // I will strictly check for validity to respect "validates these temporary points".
                 if (_currentInPoint == null || _currentOutPoint == null) return;
             }
             
             // Ensure Out > In
             var inTime = _currentInPoint.Value;
             var outTime = _currentOutPoint.Value;

             if (outTime <= inTime) 
             {
                 // Auto-correction or Fail? 
                 // Let's simple fail or correct if it's a mistake? 
                 // Previous logic was `outTime = inTime.Add(1s)`.
                 if (outTime <= inTime) outTime = inTime.Add(TimeSpan.FromSeconds(1));
             }

             var duration = outTime - inTime;

             // Commit to List
             TaggedClips.Add(new ClipLogItem 
             {
                 InPoint = FormatTimecode(inTime),
                 OutPoint = FormatTimecode(outTime),
                 Duration = duration.ToString(@"hh\:mm\:ss"),
                 Notes = $"Clip {TaggedClips.Count + 1}"
             });

             // Reset / Cleanup
             _currentInPoint = null;
             _currentOutPoint = null;
             CurrentMarkInDisplay = "xx:xx:xx:xx";
             CurrentMarkOutDisplay = "yy:yy:yy:yy";
             IsLoggingPending = false;
        }


        [RelayCommand]
        private void ExportLogs()
        {
            if (TaggedClips.Count == 0)
            {
                 new GenericMessageWindow("No clips to export.", "Veriflow Export").ShowDialog();
                 return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Edit Decision List (*.edl)|*.edl",
                Title = "Export Logs",
                FileName = System.IO.Path.GetFileNameWithoutExtension(FileName) // Default name
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string edlPath = dialog.FileName;
                    string alePath = System.IO.Path.ChangeExtension(edlPath, ".ale");

                    // 1. Generate EDL
                    string edlContent = GenerateEdlContent();
                    File.WriteAllText(edlPath, edlContent);

                    // 2. Generate ALE
                    string aleContent = GenerateAleContent();
                    File.WriteAllText(alePath, aleContent);

                    new GenericMessageWindow($"Export Successful!\n\nEDL: {edlPath}\nALE: {alePath}", "Veriflow Export").ShowDialog();
                }
                catch (Exception ex)
                {
                    new GenericMessageWindow($"Export Failed: {ex.Message}", "Error").ShowDialog();
                }
            }
        }

        private string GenerateEdlContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("TITLE: VERIFLOW_EXPORT");
            sb.AppendLine("FCM: NON-DROP FRAME");
            sb.AppendLine();

            // Destination Timecode Start (01:00:00:00)
            TimeSpan destTime = new TimeSpan(1, 0, 0); 
            int index = 1;

            string reelName = SanitizeReelName(FileName);

            foreach (var clip in TaggedClips)
            {
                // Format: 001  REELNAME  V     C        InPoint OutPoint DestIn DestOut
                // CMX 3600 standard usually strictly fixed width but basic spacing works for most NLEs.
                
                // Parse Clip Times
                // ClipLogItem stores formatted string "HH:mm:ss:ff". Only need to sanitize/pass through.
                string srcIn = clip.InPoint;
                string srcOut = clip.OutPoint;

                // Calculate Duration to update DestOut
                // We need to parse strict timecode to add duration? 
                // Or simplistic: Just re-use duration string logic?
                // Let's rely on Timecode parsing logic if we want perfectly continuous DestTC.
                // Simplified approach: Re-calculate Dest Out based on Duration.
                
                TimeSpan durationTs = TimeSpan.Zero;
                if (TimeSpan.TryParse(clip.Duration, out var d)) durationTs = d;
                
                // Format Dest In
                string dstIn = FormatTimecodeForEdl(destTime);
                
                destTime += durationTs;
                string dstOut = FormatTimecodeForEdl(destTime); // This is technically inaccurate if not accounting for frames logic vs ms, but consistent with app logic.

                sb.AppendLine($"{index:D3}  {reelName,-8} V     C        {srcIn} {srcOut} {dstIn} {dstOut}");
                sb.AppendLine($"* FROM CLIP NAME: {clip.Notes}"); // Comment
                sb.AppendLine();

                index++;
            }

            return sb.ToString();
        }

        private string GenerateAleContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Heading");
            sb.AppendLine("FIELD_DELIM:\tTABS");
            sb.AppendLine("VIDEO_FORMAT:\t1080"); // Generic default
            sb.AppendLine($"FPS:\t{_fps:F2}");
            sb.AppendLine();
            
            sb.AppendLine("Name\tTracks\tStart\tEnd\tDuration"); // Columns
            sb.AppendLine("Data");

            string reelName = SanitizeReelName(FileName);

            foreach (var clip in TaggedClips)
            {
                // Name (Notes) | Tracks (V) | Start | End | Duration
                sb.AppendLine($"{clip.Notes}\tV\t{clip.InPoint}\t{clip.OutPoint}\t{clip.Duration}");
            }

            return sb.ToString();
        }

        private string SanitizeReelName(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return "AX";
            string name = System.IO.Path.GetFileNameWithoutExtension(filename);
            // EDL Reel names max 8 chars usually, sometimes strictly alphanumeric.
            if (name.Length > 8) name = name.Substring(name.Length - 8); // Take last 8 chars usually better for uniqueness? Or first 8?
            // Let's take first 8 for readability.
            if (name.Length > 8) name = name.Substring(0, 8);
            return name.Replace(" ", "").ToUpper();
        }

        private string FormatTimecodeForEdl(TimeSpan ts)
        {
            // Re-using exiting logic but ensuring strict formatting if needed.
            // Our existing FormatTimecode logic relies on _startHeaderOffset. 
            // For DestTC, we just passed a raw TimeSpan starting at 1h.
            // We can just format it manually similar to FormatTimecode but without offset.
             double totalSeconds = ts.TotalSeconds;
             int h = (int)ts.TotalHours; 
             int m = ts.Minutes;
             int s = ts.Seconds;
             int frames = (int)Math.Round((totalSeconds - (int)totalSeconds) * _fps);
             if (frames >= _fps) frames = 0;

             return $"{h:D2}:{m:D2}:{s:D2}:{frames:D2}";
        }

        public void Dispose()
        {
            // Stop all timers
            _uiTimer?.Stop();
            _delayTimer?.Stop();
            _jogTimer?.Stop();
            
            // Unsubscribe from MediaPlayer events to prevent memory leaks
            if (_mediaPlayer != null)
            {
                _mediaPlayer.LengthChanged -= OnLengthChanged;
                _mediaPlayer.EndReached -= OnEndReached;
                _mediaPlayer.EncounteredError -= OnError;
                
                // Dispose media and player
                _mediaPlayer.Media?.Dispose();
                _mediaPlayer.Dispose();
            }
            
            // Unsubscribe from timer events
            if (_uiTimer != null)
            {
                _uiTimer.Tick -= OnUiTick;
            }
            
            // Note: _delayTimer and _jogTimer use anonymous lambdas, 
            // so we can't unsubscribe them individually.
            // Stopping and nulling them is sufficient as they're local to this instance.
            
            // _libVLC should NOT be disposed here as it is shared via VideoEngineService
            
            // Suppress finalization
            GC.SuppressFinalize(this);
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

