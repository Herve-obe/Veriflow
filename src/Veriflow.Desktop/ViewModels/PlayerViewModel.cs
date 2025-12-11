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

        public void Dispose()
        {
            MediaPlayer?.Dispose();
        }
        
#pragma warning disable CS0067
        public event Action<System.Collections.Generic.IEnumerable<string>>? RequestTranscode;
#pragma warning restore CS0067
    }
}
