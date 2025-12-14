using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Veriflow.Desktop.Views.Shared
{
    public partial class TransportControls : UserControl
    {
        public TransportControls()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ShowVolumeProperty = 
            DependencyProperty.Register("ShowVolume", typeof(bool), typeof(TransportControls), new PropertyMetadata(true));

        public bool ShowVolume
        {
            get { return (bool)GetValue(ShowVolumeProperty); }
            set { SetValue(ShowVolumeProperty, value); }
        }

        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                // Scrubber Logic: Pause Timer if supported by ViewModel
                if (slider.Tag?.ToString() == "Scrubber")
                {
                    dynamic vm = DataContext;
                    // Check if method exists (simplistic dynamic dispatch allow it to fail silently or throw if not present, 
                    // but in this controlled env, we assume ViewModels have it if they use this control for scrubbing)
                    // However, TransportControl usually doesn't have the scrubber inside it in the original VideoView. 
                    // WAIT: The scrubber was separate in VideoView (Grid.Row=3). 
                    // In the TransportControls XAML I pasted, there IS NO Scrubber. Only Volume. 
                    // BUT I should keep the logic generic just in case or for volume sliding?
                    // Actually, volume sliding doesn't need BeginSeek/EndSeek. 
                    // So simple slider update is enough for Volume.
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

        private void Slider_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Slider slider && slider.IsMouseCaptured)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    UpdateSliderValue(slider, e);
                }
                else
                {
                    slider.ReleaseMouseCapture();
                }
            }
        }

        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                slider.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void UpdateSliderValue(Slider slider, MouseEventArgs e)
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
