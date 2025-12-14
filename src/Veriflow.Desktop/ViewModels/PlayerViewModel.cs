using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using System;
using Veriflow.Desktop.Services;

namespace Veriflow.Desktop.ViewModels
{
    public partial class PlayerViewModel : ObservableObject, IDisposable
    {
        public MediaPlayer MediaPlayer { get; private set; }

        public bool ShowVolumeControls => false; // Audio Player uses external mixer console

        private float _volume = 1.0f;
        private float _preMuteVolume = 1.0f; // Store volume before muting

        public float Volume
        {
            get => _volume;
            set
            {
                if (SetProperty(ref _volume, value) && MediaPlayer != null)
                {
                    MediaPlayer.Volume = (int)(value * 100);

                    // "Unmute on Drag" feature
                    if (value > 0 && IsMuted)
                    {
                        IsMuted = false;
                        // When unmuting via drag, we do NOT restore _preMuteVolume
                        // The IsMuted setter handle this check.
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
                if (SetProperty(ref _isMuted, value) && MediaPlayer != null)
                {
                    MediaPlayer.Mute = value;

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
                        // Restore only if creating logical Unmute (not via Drag)
                        // If Volume is > 0, it means we are likely dragging or set it manually
                        if (Volume == 0)
                        {
                             Volume = _preMuteVolume > 0.05f ? _preMuteVolume : 0.5f; // Default to 50% causes if too low
                        }
                    }
                }
            }
        }

        public PlayerViewModel()
        {
            if (VideoEngineService.Instance.LibVLC == null)
            {
                VideoEngineService.Instance.Initialize();
            }

            MediaPlayer = new MediaPlayer(VideoEngineService.Instance.LibVLC!);
        }

        [RelayCommand]
        private void TogglePlayPause()
        {
            if (MediaPlayer.IsPlaying)
            {
                MediaPlayer.Pause();
            }
            else
            {
                MediaPlayer.Play();
            }
        }

        [RelayCommand]
        private void ToggleMute()
        {
            IsMuted = !IsMuted;
        }

        public void Dispose()
        {
            MediaPlayer?.Dispose();
        }
        
#pragma warning disable CS0067
        public event Action<System.Collections.Generic.IEnumerable<string>>? RequestTranscode;
#pragma warning restore CS0067
    }
}
