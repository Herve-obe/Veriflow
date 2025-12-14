using System.Windows.Controls;

namespace Veriflow.Desktop.Views
{
    public partial class AudioPlayerView : UserControl
    {
        public AudioPlayerView()
        {
            InitializeComponent();
            Loaded += (s, e) => Focus();
        }

        private void Slider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                // Allow DoubleClick to bubble up (for Reset Command)
                if (e.ClickCount == 2) return;

                // Disable IsMoveToPointEnabled in code if set in XAML, 
                // but better to remove it in XAML.
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
                    // Safety release if button not pressed
                    slider.ReleaseMouseCapture();
                }
            }
        }
        
        // Safety for lost capture
        private void Slider_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
             // Optional logic
        }

        private void Slider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                slider.ReleaseMouseCapture();
                // Do not mark as handled, allows bubble up for Click/DoubleClick state
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
