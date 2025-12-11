using System.Windows;
using System.Windows.Controls;
using Veriflow.Desktop.ViewModels;

namespace Veriflow.Desktop.Views
{
    public partial class VideoPlayerView : UserControl
    {
        public VideoPlayerView()
        {
            InitializeComponent();
            
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AttachMediaPlayer();
            this.Focus();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Detach to prevent memory leaks and "stolen" instance issues
            if (VideoViewControl != null)
            {
                VideoViewControl.MediaPlayer = null;
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachMediaPlayer();
        }

        private void AttachMediaPlayer()
        {
            // Defensive coding: Check if UI is ready and DataContext is correct
            if (VideoViewControl == null) return;

            if (DataContext is VideoPlayerViewModel vm && vm.Player != null)
            {
                // Only attach if not already attached to avoid flickering
                if (VideoViewControl.MediaPlayer != vm.Player)
                {
                    VideoViewControl.MediaPlayer = vm.Player;
                }
            }
        }
    }
}
