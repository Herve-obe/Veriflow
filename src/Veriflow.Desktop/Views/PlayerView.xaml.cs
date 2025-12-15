using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Veriflow.Desktop.Views
{
    public partial class PlayerView : UserControl
    {
        public PlayerView()
        {
            InitializeComponent();
        }

        // Ensure the UserControl takes focus when clicked (fixes shortcut issues)
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Only take focus if not clicking a focusable child (like a TextBox)
            // But since KeyBindings are on this dedicated UC, forcing focus here is usually safe 
            // as long as we don't steal from specific input fields.
            // Check original source?
            if (!(e.OriginalSource is TextBox || e.OriginalSource is PasswordBox))
            {
                 Focus();
            }
        }

        // --- SLIDER INTERACTION LOGIC (Migrated from TransportControls) ---

        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                // Force Capture for "Jump to Click" behavior
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
