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
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;

        // New Master Stereo Control
        [ObservableProperty]
        private float _leftVolume = 1.0f;

        [ObservableProperty]
        private float _rightVolume = 1.0f;

        [ObservableProperty]
        private bool _isLeftMuted;

        [ObservableProperty]
        private bool _isRightMuted;

        partial void OnLeftVolumeChanged(float value) => UpdateVolume();
        partial void OnRightVolumeChanged(float value) => UpdateVolume();
        partial void OnIsLeftMutedChanged(bool value) => UpdateVolume();
        partial void OnIsRightMutedChanged(bool value) => UpdateVolume();

        private void UpdateVolume()
        {
            if (_mediaPlayer != null)
            {
                 // Native Direct Audio doesn't support easy L/R independent volume without filters.
                 // We will map the Right Volume slider to Master Volume for now, 
                 // or take the max of both to ensure audible output.
                 var vol = Math.Max(LeftVolume, RightVolume);
                 if (IsLeftMuted && IsRightMuted) vol = 0;
                 
                 _mediaPlayer.Volume = (int)(vol * 100);
                 _mediaPlayer.Mute = (IsLeftMuted && IsRightMuted);
            }
        }

        [ObservableProperty]
        private MediaPlayer? _player; // Expose to View for Binding

        [ObservableProperty]
        private string _filePath = "No file loaded";

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
        private string _currentTimeDisplay = "00:00:00";

        [ObservableProperty]
        private string _totalTimeDisplay = "00:00:00";

        [ObservableProperty]
        private double _playbackPercent;

        [ObservableProperty]
        private VideoMetadata _currentVideoMetadata = new();

        public event Action<IEnumerable<string>>? RequestTranscode;
        // public event Action<string>? RequestOffloadSource;

        // Timer for updating UI slider/time
        private readonly DispatcherTimer _uiTimer;
        private bool _isUserSeeking; // Prevent timer updates while dragging slider

        public VideoPlayerViewModel()
        {
            if (!DesignMode.IsDesignMode)
            {
                LibVLCSharp.Shared.Core.Initialize();
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);
                Player = _mediaPlayer; 
                
                _mediaPlayer.LengthChanged += OnLengthChanged;
                _mediaPlayer.EndReached += OnEndReached;
                _mediaPlayer.EncounteredError += OnError;
            }

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _uiTimer.Tick += OnUiTick;
        }

        private void InitializeAudioEngine(int channels)
        {
            // Direct Audio Mode: Just set volume
            UpdateVolume();
        }

        private void StopAudioEngine()
        {
            // No custom engine to stop
        }

        private void OnUiTick(object? sender, EventArgs e)
        {
            if (_mediaPlayer != null && _mediaPlayer.IsPlaying && !_isUserSeeking)
            {
                var time = _mediaPlayer.Time;
                var length = _mediaPlayer.Length;

                if (length > 0)
                {
                    PlaybackPercent = (double)time / length;
                }
                
                CurrentTimeDisplay = TimeSpan.FromMilliseconds(time).ToString(@"hh\:mm\:ss");
            }
        }

        private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TotalTimeDisplay = TimeSpan.FromMilliseconds(e.Length).ToString(@"hh\:mm\:ss");
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
                CurrentTimeDisplay = "00:00:00";
                
                // Reset player for replay
                _mediaPlayer?.Stop(); 
                
                // Also ensure StopPressed logic if handled here?
                // The Stop command handles explicit stop. EndReached is automatic.
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
            try
            {
                await Stop(); // Stop any existing playback

                FilePath = path;
                FileName = System.IO.Path.GetFileName(path);

                if (_libVLC != null && _mediaPlayer != null)
                {
                    var media = new Media(_libVLC, path, FromType.FromPath);
                    await media.Parse(MediaParseOptions.ParseLocal);
                    _mediaPlayer.Media = media;
                    
                    // Pre-load metadata via FFprobe
                    await LoadMetadataWithFFprobe(path);
                    
                    // Init Audio Engine based on metadata channels
                    UpdateAudioTrackState();
                    
                    IsVideoLoaded = true;
                    // Trigger initial duration update if possible
                    if (media.Duration > 0)
                    {
                         TotalTimeDisplay = TimeSpan.FromMilliseconds(media.Duration).ToString(@"hh\:mm\:ss");
                    }
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
        }

        private void UpdateAudioTrackState()
        {
            int channelCount = 2; // Default Stereo
            // Parse "X Channels" or "2"
            if (!string.IsNullOrWhiteSpace(CurrentVideoMetadata.AudioChannels))
            {
                var parts = CurrentVideoMetadata.AudioChannels.Split(' ');
                if (parts.Length > 0 && int.TryParse(parts[0], out int c))
                {
                    channelCount = c;
                }
                else if (parts.Length > 0 && parts[0].Contains(".")) // e.g. 5.1
                {
                    if(parts[0] == "5.1") channelCount = 6;
                    if(parts[0] == "7.1") channelCount = 8;
                }
            }

            // Init Engine
            InitializeAudioEngine(channelCount);
        }

        [RelayCommand(CanExecute = nameof(CanPlay))]
        private void Play()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Play();
                _uiTimer.Start();
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
                _uiTimer.Stop();
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
            IsPlaying = false;
            IsPaused = false;
            PlaybackPercent = 0;
            CurrentTimeDisplay = "00:00:00";
            
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
                CurrentTimeDisplay = TimeSpan.FromMilliseconds(time).ToString(@"hh\:mm\:ss");
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
                CurrentTimeDisplay = TimeSpan.FromMilliseconds(time).ToString(@"hh\:mm\:ss");
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
                         CurrentTimeDisplay = TimeSpan.FromMilliseconds(seekTime).ToString(@"hh\:mm\:ss");
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
                    // Valid video extensions
                    if (new[] { ".mp4", ".mov", ".mxf", ".avi", ".mkv", ".mts", ".ts" }.Contains(ext))
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
             CurrentTimeDisplay = "00:00:00";
             TotalTimeDisplay = "00:00:00";
             PlaybackPercent = 0;
             CurrentVideoMetadata = new VideoMetadata(); // Clear metadata
             
             StopAudioEngine();
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

        public void Dispose()
        {
            StopAudioEngine();
            _uiTimer.Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
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
