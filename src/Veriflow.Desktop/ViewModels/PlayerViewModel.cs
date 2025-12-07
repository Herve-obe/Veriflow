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
        private AudioFileReader? _audioFile;
        private MultiChannelAudioMixer? _mixer;
        private readonly DispatcherTimer _playbackTimer;

        [ObservableProperty]
        private string _filePath = "Aucun fichier chargé";

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
        private AudioMetadata _currentMetadata = new();

        public ObservableCollection<TrackViewModel> Tracks { get; } = new();

        public PlayerViewModel()
        {
            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // Faster for meters if we had them
            };
            _playbackTimer.Tick += OnTimerTick;
        }

        [RelayCommand]
        private void LoadFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Audio Files (*.wav;*.bwf)|*.wav;*.bwf|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadAudio(openFileDialog.FileName);
            }
        }

        public void LoadAudio(string path)
        {
            try
            {
                Stop();
                CleanUpAudio();

                _audioFile = new AudioFileReader(path);
                
                // 1. Downmix to Stereo (keeps source SampleRate)
                _mixer = new MultiChannelAudioMixer(_audioFile);

                ISampleProvider finalProvider = _mixer;

                // 2. Resample to 48kHz if needed (Universal Compatibility)
                if (finalProvider.WaveFormat.SampleRate != 48000)
                {
                    try
                    {
                         finalProvider = new WdlResamplingSampleProvider(finalProvider, 48000);
                    }
                    catch
                    {
                        // Fallback or ignore if WDL not available (should be in NAudio)
                        // If WDL fails, we might try MediaFoundationResampler but that requires WaveProvider conversion.
                        // For this iteration, we assume WdlResamplingSampleProvider is available as part of NAudio.
                    }
                }

                _outputDevice = new WaveOutEvent();
                _outputDevice.Init(finalProvider);
                _outputDevice.PlaybackStopped += OnPlaybackStopped;

                FilePath = path;
                FileName = System.IO.Path.GetFileName(path);
                IsAudioLoaded = true;

                TotalTimeDisplay = _audioFile.TotalTime.ToString(@"hh\:mm\:ss");
                PlaybackMaximum = _audioFile.TotalTime.TotalSeconds;

                // Metadata Extraction
                var metaReader = new BwfMetadataReader();
                CurrentMetadata = metaReader.ReadMetadataFromStream(path);

                InitializeTracks(CurrentMetadata.ChannelCount > 0 ? CurrentMetadata.ChannelCount : _audioFile.WaveFormat.Channels);
                GenerateWaveforms(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors du chargement : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
                
                // Note: iXML track count might not match file channel count (e.g. poly file has blank channels)
                // We rely on channelCount from the WaveFormat or BWF header.
                
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
            // User mute toggle. 
            // In a real console, User Mute + Solo Logic are combined.
            // Here, we just re-evaluate the mixer state based on everything.
            UpdateMixerState();
        }

        private void OnTrackSoloChanged(TrackViewModel track)
        {
            // Re-evaluate mixer state based on Solos
            UpdateMixerState();
        }

        private void UpdateMixerState()
        {
            if (_mixer == null) return;

            foreach (var track in Tracks)
            {
                // Push states to mixer property
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
                        // We need to find the MAX for each channel in this chunk
                        for (int c = 0; c < totalChannels; c++)
                        {
                            float max = 0;
                            // Stride is totalChannels
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
                            // Height of the visual area per track is 40, Center is 20
                            // Center is 20
                            
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
                });
            });
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            IsPlaying = false;
            _playbackTimer.Stop();
            PlaybackPosition = 0;
            CurrentTimeDisplay = "00:00:00";
            if (_audioFile != null)
                _audioFile.Position = 0;
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_audioFile != null)
            {
                CurrentTimeDisplay = _audioFile.CurrentTime.ToString(@"hh\:mm\:ss");
                PlaybackPosition = _audioFile.CurrentTime.TotalSeconds;
            }
        }

        [RelayCommand(CanExecute = nameof(CanPlay))]
        private void Play()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Play();
                _playbackTimer.Start();
                IsPlaying = true;
            }
        }

        [RelayCommand(CanExecute = nameof(CanStop))]
        private void Stop()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Stop();
            }
        }

        [RelayCommand]
        private void Pause()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Pause();
                _playbackTimer.Stop();
                IsPlaying = false;
            }
        }

        private bool CanPlay() => IsAudioLoaded && !IsPlaying;
        private bool CanStop() => IsAudioLoaded; // Allow stop even if not playing (to reset pos)

        private void CleanUpAudio()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Dispose();
                _outputDevice = null;
            }
            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }
            // Mixer is light wrapper, no dispose needed usually but good practice to clear ref
            _mixer = null;
        }

        public void Dispose()
        {
            CleanUpAudio();
        }
    }
}
