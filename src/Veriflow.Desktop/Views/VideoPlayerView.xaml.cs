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
             // Do NOT Stop() here if we want to Resume. 
             // Just detach the VideoView to clean up the Vout/HWND connection.
             
            // Detach to prevent memory leaks and "stolen" instance issues
            if (VideoViewControl != null)
            {
                VideoViewControl.MediaPlayer = null;
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
             if (this.IsLoaded)
             {
                 AttachMediaPlayer();
             }
        }

        private void AttachMediaPlayer()
        {
            // Defensive coding: Check if UI is ready and DataContext is correct
            if (VideoViewControl == null) return;

            if (DataContext is VideoPlayerViewModel vm && vm.Player != null)
            {
                // Defer attachment to ensure HWND is ready (fixes Black Screen on tab switch)
                // Defer attachment to ensure HWND is ready (fixes Black Screen on tab switch)
                Dispatcher.BeginInvoke(new Action(() => 
                {
                    // FORCE Re-attach even if same instance to refresh HWND linkage
                    if (VideoViewControl != null)
                    {
                        VideoViewControl.MediaPlayer = null; 
                        VideoViewControl.MediaPlayer = vm.Player;
                    }

                    // FORCE FRAME REPAINT
                    // If paused, the Vout might render black until the next frame decode.
                    // NextFrame() forces the pipeline to decode and render one frame to the new Vout.
                    if (vm.Player != null && vm.Player.State == LibVLCSharp.Shared.VLCState.Paused)
                    {
                        vm.Player.NextFrame();
                    }
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }

        // --- SLIDER LOGIC ---

        private void Slider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                // Scrubber Logic: Pause Timer
                if (slider.Tag?.ToString() == "Scrubber" && DataContext is VideoPlayerViewModel vm)
                {
                    vm.BeginSeek();
                }

                // Force Capture
                bool captured = slider.CaptureMouse();
                if (captured)
                {
                    UpdateSliderValue(slider, e);
                    e.Handled = true;
                }
            }
        }

        private void Slider_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Slider slider && slider.IsMouseCaptured)
            {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    UpdateSliderValue(slider, e);
                }
                else
                {
                    slider.ReleaseMouseCapture();
                    // Recover EndSeek if lost capture
                     if (slider.Tag?.ToString() == "Scrubber" && DataContext is VideoPlayerViewModel vm)
                    {
                        vm.EndSeek();
                    }
                }
            }
        }

        private void Slider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                slider.ReleaseMouseCapture();

                if (slider.Tag?.ToString() == "Scrubber" && DataContext is VideoPlayerViewModel vm)
                {
                    vm.EndSeek();
                }

                e.Handled = true;
            }
        }

        private void UpdateSliderValue(Slider slider, System.Windows.Input.MouseEventArgs e)
        {
            var point = e.GetPosition(slider);
            var width = slider.ActualWidth;
            if (width > 0)
            {
                double percent = point.X / width;
                if (percent < 0) percent = 0;
                if (percent > 1) percent = 1;

                double range = slider.Maximum - slider.Minimum;
                double value = slider.Minimum + (range * percent);
                slider.Value = value;
            }
        }
    }
}
