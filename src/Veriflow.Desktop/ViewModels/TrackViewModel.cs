using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Media;

namespace Veriflow.Desktop.ViewModels
{
    public partial class TrackViewModel : ObservableObject
    {
        public int ChannelIndex { get; }
        private readonly Action<TrackViewModel> _onSoloRequested;
        private readonly Action<int, float> _onVolumeChanged;
        private readonly Action<int, bool> _onMuteChanged;
        private readonly Action<int, float> _onPanChanged;

        [ObservableProperty]
        private string _trackName;

        [ObservableProperty]
        private float _volume = 1.0f;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsActive))]
        private bool _isMuted;

        [ObservableProperty]
        private bool _isSoloed;

        [ObservableProperty]
        private float _pan = 0.0f; // -1.0 to 1.0
        
        // Visuals
        [ObservableProperty]
        private PointCollection _waveformPoints = new();

        public bool IsActive => !IsMuted;

        public TrackViewModel(int channelIndex, string defaultName, 
                              Action<TrackViewModel> onSoloRequested, 
                              Action<int, float> onVolumeChanged, 
                              Action<int, bool> onMuteChanged,
                              Action<int, float> onPanChanged)
        {
            ChannelIndex = channelIndex;
            TrackName = defaultName;
            _onSoloRequested = onSoloRequested;
            _onVolumeChanged = onVolumeChanged;
            _onMuteChanged = onMuteChanged;
            _onPanChanged = onPanChanged;
        }

        partial void OnVolumeChanged(float value)
        {
            _onVolumeChanged?.Invoke(ChannelIndex, value);
        }

        partial void OnIsMutedChanged(bool value)
        {
            _onMuteChanged?.Invoke(ChannelIndex, value);
        }

        partial void OnIsSoloedChanged(bool value)
        {
            _onSoloRequested?.Invoke(this);
        }

        partial void OnPanChanged(float value)
        {
            _onPanChanged?.Invoke(ChannelIndex, value);
        }

        [RelayCommand]
        private void ResetVolume()
        {
            Volume = 1.0f;
        }

        [RelayCommand]
        private void ResetPan()
        {
            Pan = 0.0f;
        }
    }
}
