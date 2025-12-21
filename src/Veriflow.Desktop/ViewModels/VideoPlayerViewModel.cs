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

        [ObservableProperty]
        private ClipLogItem? _editingClip;

        private ClipLogItem? _originalClipData;

        private TimeSpan? _currentInPoint;
        private TimeSpan? _currentOutPoint;

        [ObservableProperty]
        private string _currentMarkInDisplay = "xx:xx:xx:xx";

        [ObservableProperty]
        private string _currentMarkOutDisplay = "yy:yy:yy:yy";



        [ObservableProperty]
        private bool _isLoggingPending;

        public event Action<IEnumerable<string>>? RequestTranscode;

        // Callbacks / Events
        public Action<ClipLogItem>? AddClipToReportCallback { get; set; }
        public event Action? FlashMarkInButton;
        public event Action? FlashMarkOutButton;
        public event Action? FlashTagClipButton;

        // --- FILE NAVIGATION ---
        private readonly FileNavigationService _fileNavigationService = new();
        private static readonly string[] VideoExtensions = { ".mov", ".mp4", ".mxf", ".avi", ".mkv", ".m4v", ".mpg", ".mpeg" };
        private List<string> _siblingFiles = new();
        private int _currentFileIndex = -1;

        
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

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // 60fps - optimal for smooth UI, matches display refresh rate
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
            return Services.TimecodeHelper.FormatTimecode(time, _fps, _startHeaderOffset);
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




        public async System.Threading.Tasks.Task LoadVideo(string path)
        {
             System.Diagnostics.Debug.WriteLine($"LoadVideo: START - path = {path}");
             
             // Set Path first so Refresh can use it
             FilePath = path;
             System.Diagnostics.Debug.WriteLine($"LoadVideo: FilePath set");
             
             RefreshReportLink();
             System.Diagnostics.Debug.WriteLine($"LoadVideo: RefreshReportLink completed");
             
             await LoadMediaContext(path);
             System.Diagnostics.Debug.WriteLine($"LoadVideo: LoadMediaContext completed");
             
             // Update sibling files for navigation
             UpdateSiblingFiles(path);
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
                IsVideoLoaded = true;

                // Load metadata
                await LoadMetadataWithFFprobe(path);

                // Track in recent files
                Services.RecentFilesService.Instance.AddRecentFile(path);

                // ProRes RAW Detection - Prevent playback
                if (CurrentVideoMetadata.IsProResRAW)
                {
                    IsVideoLoaded = false;
                    System.Windows.MessageBox.Show(
                        CurrentVideoMetadata.ProResRAWMessage,
                        "ProRes RAW Not Supported",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

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
                    System.Diagnostics.Debug.WriteLine("LoadMediaContext: Play() called");
                    
                    // Update State to reflect "Paused at Start"
                    // Note: We set IsPaused to FALSE because this is an auto-pause for preloading,
                    // not a user-initiated pause. The pause button should not be active.
                    IsPlaying = false;
                    IsPaused = false;

                    // Trigger initial duration update if possible (from original method)
                    if (media.Duration > 0)
                    {
                         TotalTimeDisplay = FormatTimecode(TimeSpan.FromMilliseconds(media.Duration));
                    }

                    // Load heavy metadata in background (from original method)
                    // The UI is already active
                    await LoadMetadataWithFFprobe(path);
                    
                    // Initial Volume Set (Force update) (from original method)
                    _mediaPlayer.Volume = (int)(Volume * 100);
                    _mediaPlayer.Mute = IsMuted;

                    System.Diagnostics.Debug.WriteLine("LoadMediaContext: COMPLETED SUCCESSFULLY");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadMediaContext: EXCEPTION - {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"LoadMediaContext: Stack trace - {ex.StackTrace}");
                System.Windows.MessageBox.Show($"Error loading video: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task LoadMetadataWithFFprobe(string path)
        {
             var provider = new FFprobeMetadataProvider();
             CurrentVideoMetadata = await provider.GetVideoMetadataAsync(path);
             
              // Parse FPS
              double parsedFps = Services.TimecodeHelper.ParseFrameRate(CurrentVideoMetadata.FrameRate);
              if (parsedFps > 0) _fps = parsedFps;

              // Parse Start Timecode Offset
              _startHeaderOffset = Services.TimecodeHelper.ParseTimecodeOffset(CurrentVideoMetadata.StartTimecode, _fps);
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
                var time = _mediaPlayer.Time - 1000; // 1 second jump
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
                var time = _mediaPlayer.Time + 1000; // 1 second jump
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
             IsPlaying = false;
             IsPaused = false; // Reset pause state
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

            // Handle plain arrow keys for jog (frame-by-frame)
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
            // Only stop jog for plain arrow keys
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
            
            var currentTime = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
            var formattedTime = FormatTimecode(currentTime);
            
            // If editing a clip, update its Mark IN
            if (EditingClip != null)
            {
                EditingClip.InPoint = formattedTime;
                RecalculateClipDuration(EditingClip);
            }
            else
            {
                // Normal mode: set temporary Mark IN for new clip
                _currentInPoint = currentTime;
                CurrentMarkInDisplay = formattedTime;
                IsLoggingPending = true;
            }
            
            // Trigger button flash animation
            FlashMarkInButton?.Invoke();
        }

        [RelayCommand]
        private void SetOut()
        {
            if (_mediaPlayer == null || !IsVideoLoaded) return;
            
            var currentTime = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
            var formattedTime = FormatTimecode(currentTime);
            
            // If editing a clip, update its Mark OUT
            if (EditingClip != null)
            {
                EditingClip.OutPoint = formattedTime;
                RecalculateClipDuration(EditingClip);
            }
            else
            {
                // Normal mode: set temporary Mark OUT for new clip
                _currentOutPoint = currentTime;
                CurrentMarkOutDisplay = formattedTime;
                IsLoggingPending = true;
            }
            
            // Trigger button flash animation
            FlashMarkOutButton?.Invoke();
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

             // Create clip
             var clip = new ClipLogItem 
             {
                 InPoint = FormatTimecode(inTime),
                 OutPoint = FormatTimecode(outTime),
                 Duration = duration.ToString(@"hh\:mm\:ss"),
                 Notes = $"Clip {TaggedClips.Count + 1}",
                 SourceFile = FilePath ?? "" // Track which rush this clip belongs to
             };

             // Add to local list (for current session display)
             TaggedClips.Add(clip);
             ExportLogsCommand.NotifyCanExecuteChanged();

             // ← NEW: Send to Report for multi-rush logging
             AddClipToReportCallback?.Invoke(clip);

             // Reset / Cleanup
             _currentInPoint = null;
             _currentOutPoint = null;
             CurrentMarkInDisplay = "xx:xx:xx:xx";
             CurrentMarkOutDisplay = "yy:yy:yy:yy";
             IsLoggingPending = false;
             
             // Trigger button flash animation
             FlashTagClipButton?.Invoke();
        }


        [RelayCommand(CanExecute = nameof(CanExportLogs))]
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

        private bool CanExportLogs() => TaggedClips.Count > 0;

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

        // --- CLIP EDITING COMMANDS ---

        [RelayCommand]
        private void EnterEditMode(ClipLogItem clip)
        {
            if (clip == null) return;

            // Exit current edit mode if any
            if (EditingClip != null)
            {
                EditingClip.IsEditing = false;
            }

            // Backup original data for cancel
            _originalClipData = new ClipLogItem
            {
                InPoint = clip.InPoint,
                OutPoint = clip.OutPoint,
                Duration = clip.Duration,
                Notes = clip.Notes,
                TagColor = clip.TagColor
            };

            // Enter edit mode
            EditingClip = clip;
            EditingClip.IsEditing = true;
        }

        [RelayCommand]
        private void SaveClipEdit()
        {
            if (EditingClip == null) return;

            // Exit edit mode
            EditingClip.IsEditing = false;
            EditingClip = null;
            _originalClipData = null;
        }

        [RelayCommand]
        private void CancelClipEdit()
        {
            if (EditingClip == null || _originalClipData == null) return;

            // Restore original values
            EditingClip.InPoint = _originalClipData.InPoint;
            EditingClip.OutPoint = _originalClipData.OutPoint;
            EditingClip.Duration = _originalClipData.Duration;
            EditingClip.Notes = _originalClipData.Notes;
            EditingClip.TagColor = _originalClipData.TagColor;

            // Exit edit mode
            EditingClip.IsEditing = false;
            EditingClip = null;
            _originalClipData = null;
        }

        [RelayCommand]
        private void DeleteClip(ClipLogItem clip)
        {
            if (clip == null) return;

            // If deleting the clip being edited, exit edit mode
            if (EditingClip == clip)
            {
                EditingClip = null;
                _originalClipData = null;
            }

            TaggedClips.Remove(clip);
            ExportLogsCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        public void ClearLoggedClips()
        {
            if (TaggedClips.Count > 0)
            {
                TaggedClips.Clear();
                ExportLogsCommand.NotifyCanExecuteChanged();
            }
        }

        private void RecalculateClipDuration(ClipLogItem clip)
        {
            if (clip == null) return;

            try
            {
                var inTime = ParseTimecode(clip.InPoint);
                var outTime = ParseTimecode(clip.OutPoint);

                if (inTime.HasValue && outTime.HasValue && outTime > inTime)
                {
                    var duration = outTime.Value - inTime.Value;
                    clip.Duration = duration.ToString(@"hh\:mm\:ss");
                }
            }
            catch
            {
                // Invalid timecode format, keep current duration
            }
        }

        private TimeSpan? ParseTimecode(string timecode)
        {
            if (string.IsNullOrWhiteSpace(timecode)) return null;

            // Expected format: HH:MM:SS:FF
            var parts = timecode.Split(':');
            if (parts.Length != 4) return null;

            if (int.TryParse(parts[0], out int hours) &&
                int.TryParse(parts[1], out int minutes) &&
                int.TryParse(parts[2], out int seconds))
            {
                return new TimeSpan(hours, minutes, seconds);
            }

            return null;
        }

        // --- FILE NAVIGATION COMMANDS ---

        [RelayCommand(CanExecute = nameof(CanNavigatePrevious))]
        private async Task NavigatePrevious()
        {
            if (_currentFileIndex > 0)
            {
                await LoadVideo(_siblingFiles[_currentFileIndex - 1]);
            }
        }

        private bool CanNavigatePrevious() => _currentFileIndex > 0;

        [RelayCommand(CanExecute = nameof(CanNavigateNext))]
        private async Task NavigateNext()
        {
            if (_currentFileIndex < _siblingFiles.Count - 1)
            {
                await LoadVideo(_siblingFiles[_currentFileIndex + 1]);
            }
        }

        private bool CanNavigateNext() => _currentFileIndex >= 0 && _currentFileIndex < _siblingFiles.Count - 1;

        private void UpdateSiblingFiles(string currentPath)
        {
            (_siblingFiles, _currentFileIndex) = _fileNavigationService.GetSiblingFiles(currentPath, VideoExtensions);
            
            // Update command states
            NavigatePreviousCommand.NotifyCanExecuteChanged();
            NavigateNextCommand.NotifyCanExecuteChanged();
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

