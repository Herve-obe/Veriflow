using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
    public partial class PlayerViewModel : ObservableObject, IDisposable
    {
        private WaveOutEvent? _outputDevice;
        // _inputStream is defined below or here
        // private AudioFileReader? _audioFile; // REMOVED
        private MultiChannelAudioMixer? _mixer;
        private VeriflowMeteringProvider? _meteringProvider;
        private readonly DispatcherTimer _playbackTimer;

        [ObservableProperty]
        private string _filePath = "No file loaded";

        [ObservableProperty]
        private string _fileName = "";


        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool _isAudioLoaded;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        private bool _isPlaying;

        [ObservableProperty]
        private string _currentTimeDisplay = "00:00:00";

        [ObservableProperty]
        private string _totalTimeDisplay = "00:00:00";

        [ObservableProperty]
        private double _playbackPosition;

        [ObservableProperty]
        private double _playbackMaximum = 1;

        [ObservableProperty]
        private double _playbackPercent = 0;

        [ObservableProperty]
        private AudioMetadata _currentMetadata = new();

        public ObservableCollection<TrackViewModel> Tracks { get; } = new();

        public PlayerViewModel()
        {
            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _playbackTimer.Tick += OnTimerTick;
        }

        private bool _isTimerUpdating;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        private bool _isPaused;

        [ObservableProperty]
        private bool _isStopPressed;

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_inputStream != null)
            {
                CurrentTimeDisplay = _inputStream.CurrentTime.ToString(@"hh\:mm\:ss");
                
                _isTimerUpdating = true;
                PlaybackPosition = _inputStream.CurrentTime.TotalSeconds;
                if (PlaybackMaximum > 0)
                    PlaybackPercent = PlaybackPosition / PlaybackMaximum;
                
                // Update Meters
                // ... (code omitted for brevity, logic remains same)
                if (_meteringProvider != null)
                {
                   var peaks = _meteringProvider.ChannelPeaks;
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

        partial void OnPlaybackPositionChanged(double value)
        {
                if (_inputStream != null && !_isTimerUpdating)
            {
                // User is scrubbing
                if (value >= 0 && value <= _playbackMaximum)
                {
                    _inputStream.CurrentTime = TimeSpan.FromSeconds(value);
                    if (PlaybackMaximum > 0)
                        PlaybackPercent = value / PlaybackMaximum;
                }
            }
        }

        [RelayCommand]
        private async Task DropFile(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string file = files[0];
                    string ext = System.IO.Path.GetExtension(file).ToLower();
                    if (ext == ".wav" || ext == ".bwf")
                    {
                        await LoadAudio(file);
                    }
                }
            }
        }

        [RelayCommand]
        private async Task UnloadMedia()
        {
            await Stop();
            CleanUpAudio();

            FilePath = "No file loaded";
            FileName = "";
            IsAudioLoaded = false;
            CurrentTimeDisplay = "00:00:00";
            TotalTimeDisplay = "00:00:00";
            PlaybackPosition = 0;
            PlaybackPercent = 0;
            
            Tracks.Clear();
            RulerTicks.Clear();
            CurrentMetadata = new AudioMetadata();
        }

        [RelayCommand]
        private async Task LoadFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Audio Files (*.wav;*.bwf)|*.wav;*.bwf|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await LoadAudio(openFileDialog.FileName);
            }
        }

        private WaveStream? _inputStream; // Abstraction for AudioFileReader or MediaFoundationReader

        public async Task LoadAudio(string path)
        {
            try
            {
                await Stop();
                CleanUpAudio();

                // Try AudioFileReader first (NAudio native)
                try
                {
                    _inputStream = new AudioFileReader(path);
                }
                catch (Exception)
                {
                    // Fallback to MediaFoundationReader (Windows Codecs) for complex formats (e.g. 24-bit/32-bit float WAVs from ffmpeg)
                    try
                    {
                        _inputStream = new MediaFoundationReader(path);
                    }
                    catch
                    {
                        // If both fail, rethrow original
                        throw;
                    }
                }

                ISampleProvider finalProvider;

                if (_inputStream is AudioFileReader afr)
                {
                    finalProvider = afr;
                }
                else
                {
                    finalProvider = _inputStream.ToSampleProvider();
                }
                
                // NEW ORDER: File -> METER -> MIXER -> Resampler -> Output
                
                // 1. Metering (captures all tracks before mixing)
                _meteringProvider = new VeriflowMeteringProvider(finalProvider);

                // 2. Mixer (Downmix/Mute/Solo logic)
                _mixer = new MultiChannelAudioMixer(_meteringProvider); 

                ISampleProvider outProvider = _mixer;

                // 3. Resample to 48kHz if needed (Universal Compatibility)
                if (outProvider.WaveFormat.SampleRate != 48000)
                {
                    try
                    {
                         outProvider = new WdlResamplingSampleProvider(outProvider, 48000);
                    }
                    catch
                    {
                        // Fallback
                    }
                }

                _outputDevice = new WaveOutEvent();
                _outputDevice.Init(outProvider);
                _outputDevice.PlaybackStopped += OnPlaybackStopped;

                FilePath = path;
                FileName = System.IO.Path.GetFileName(path);
                IsAudioLoaded = true;

                TotalTimeDisplay = _inputStream.TotalTime.ToString(@"hh\:mm\:ss");
                PlaybackMaximum = _inputStream.TotalTime.TotalSeconds;

                // Metadata Extraction (BWF)
                try
                {
                    var metaReader = new BwfMetadataReader();
                    CurrentMetadata = metaReader.ReadMetadataFromStream(path);
                }
                catch { /* Ignore metadata errors */ }

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

            var totalSeconds = _inputStream.TotalTime.TotalSeconds;
            if (totalSeconds <= 0) return;

            // Absolute Start (SamplesSinceMidnight / Rate)
            double absStart = CurrentMetadata?.TimeReferenceSeconds ?? 0;

            // Determine step (e.g. every 10s, or adaptive)
            int tickCount = 10;
            double step = totalSeconds / tickCount;

            for (int i = 0; i <= tickCount; i++)
            {
                double time = i * step; // Relative time
                double absoluteTime = absStart + time;
                
                var tsRel = TimeSpan.FromSeconds(absoluteTime);
                
                RulerTicks.Add(new RulerTick
                {
                    Percent = (double)i / tickCount,
                    Label = tsRel.ToString(@"hh\:mm\:ss") // Source Timecode
                });
            }
        }

        [RelayCommand]
        private void Seek(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_inputStream == null) return;

            // Resolve position relative to the container (sender)
            if (e.Source is FrameworkElement element)
            {
                var position = e.GetPosition(element);
                var width = element.ActualWidth;

                if (width > 0)
                {
                    double percent = position.X / width;
                    double seekTime = percent * _inputStream.TotalTime.TotalSeconds;
                    
                    // Clamp
                    if (seekTime < 0) seekTime = 0;
                    if (seekTime > _inputStream.TotalTime.TotalSeconds) seekTime = _inputStream.TotalTime.TotalSeconds;

                    _inputStream.CurrentTime = TimeSpan.FromSeconds(seekTime);
                    PlaybackPosition = seekTime;
                }
            }
        }

        private void InitializeTracks(int channelCount)
        {
            Tracks.Clear();
            var iXmlNames = CurrentMetadata?.TrackNames ?? new List<string>();

            for (int i = 0; i < channelCount; i++)
            {
                string name = $"TRK {i + 1}";
                
                // Use iXML name if available
                if (i < iXmlNames.Count && !string.IsNullOrWhiteSpace(iXmlNames[i]))
                {
                    name = iXmlNames[i];
                }
                
                var track = new TrackViewModel(i, name, OnTrackSoloChanged, OnTrackVolumeChanged, OnTrackMuteChanged, OnTrackPanChanged);
                Tracks.Add(track);
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

        /// <summary>
        /// Generates waveforms for ALL tracks.
        /// </summary>
        private void GenerateWaveforms(string path)
        {
            Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await Task.Run(() =>
                {
                    try
                    {
                        using var reader = new AudioFileReader(path);
                        int totalChannels = reader.WaveFormat.Channels;
                        
                        // Resolution: 500 points wide per track
                        int width = 500;
                        long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8); 
                        long samplesPerChannel = totalSamples / totalChannels;
                        long samplesPerPoint = samplesPerChannel / width;

                        if (samplesPerPoint < 1) samplesPerPoint = 1;

                        // Data structures
                        var maxBuffers = new float[totalChannels][]; // Stores peaks
                        for(int c=0; c<totalChannels; c++) maxBuffers[c] = new float[width];

                        float[] buffer = new float[samplesPerPoint * totalChannels];
                        int pointIndex = 0;

                        while (pointIndex < width)
                        {
                            int read = reader.Read(buffer, 0, buffer.Length);
                            if (read == 0) break;

                            // Process this chunk
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

                        // Convert to Points
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            for (int c = 0; c < totalChannels && c < Tracks.Count; c++)
                            {
                                var trackPoints = new PointCollection();
                                float[] peaks = maxBuffers[c];
                                
                                for (int x = 0; x < width; x++)
                                {
                                    trackPoints.Add(new Point(x, 20 - (peaks[x] * 19))); // Top
                                }
                                
                                // Bottom mirror
                                 for (int x = width - 1; x >= 0; x--)
                                {
                                    trackPoints.Add(new Point(x, 20 + (peaks[x] * 19))); // Bottom
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
            // Redundant safety check to ensure UI matches state
            IsPlaying = false;
            _playbackTimer.Stop();
            
            // Only reset if NOT paused (Pause logic is handled in Pause command)
            // But if we came here from Stop(), IsPaused is false, so we reset.
            if (!IsPaused)
            {
                PlaybackPosition = 0;
                CurrentTimeDisplay = "00:00:00";
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
            // Visual Feedback
            IsStopPressed = true;

            // Aggressive Stop: Reset everything immediately
            _playbackTimer.Stop();
            IsPaused = false;
            IsPlaying = false;
            PlaybackPosition = 0;
            PlaybackPercent = 0;
            CurrentTimeDisplay = "00:00:00";

            if (_inputStream != null)
            {
                _inputStream.Position = 0;
            }

            // Force Meter Reset
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var t in Tracks) t.CurrentLevel = 0;
            });

            // Finally stop the device (will trigger OnPlaybackStopped, but we are already clean)
            if (_outputDevice != null)
            {
                _outputDevice.Stop();
            }

            // Reset Visual Feedback after delay
            await Task.Delay(200);
            IsStopPressed = false;
        }

        [RelayCommand]
        private void Pause()
        {
            if (_outputDevice != null)
            {
                _playbackTimer.Stop(); // Stop timer FIRST to freeze meters
                _outputDevice.Pause();
                IsPaused = true;
                IsPlaying = false;
            }
        }

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
                _inputStream.Dispose(); // Disposes the stream
                _inputStream = null;
            }
            _mixer = null;
            _meteringProvider = null;
        }

        public void Dispose()
        {
            CleanUpAudio();
        }
    }
}
