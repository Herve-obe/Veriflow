using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Veriflow.Desktop.ViewModels;

namespace Veriflow.Desktop.Views
{
    /// <summary>
    /// Interaction logic for PlayerView.xaml
    /// </summary>
    public partial class PlayerView : UserControl
    {
        public PlayerView()
        {
            InitializeComponent();
            
            // Handle drop in code-behind to persist across ViewModel switches
            this.Drop += PlayerView_Drop;
            Debug.WriteLine("PlayerView: Drop handler registered");
        }

        private async void PlayerView_Drop(object sender, DragEventArgs e)
        {
            Debug.WriteLine("PlayerView_Drop: Event triggered");
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Debug.WriteLine("PlayerView_Drop: FileDrop data present");
                
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    Debug.WriteLine($"PlayerView_Drop: Files count = {files.Length}, First file = {files[0]}");
                    
                    // Auto-switch Audio/Video mode based on file type
                    var mainVM = Application.Current.MainWindow?.DataContext as MainViewModel;
                    Debug.WriteLine($"PlayerView_Drop: MainVM = {(mainVM != null ? "Found" : "NULL")}");
                    
                    if (mainVM != null)
                    {
                        Debug.WriteLine($"PlayerView_Drop: Current mode BEFORE switch = {mainVM.CurrentAppMode}");
                        mainVM.AutoSwitchModeForFiles(files);
                        Debug.WriteLine($"PlayerView_Drop: Current mode AFTER switch = {mainVM.CurrentAppMode}");
                        
                        // Wait for mode switch to complete
                        Debug.WriteLine("PlayerView_Drop: Waiting 500ms for mode switch...");
                        await Task.Delay(500);

                        // Get the current ViewModel from MainViewModel.CurrentView (NOT this.DataContext!)
                        var currentVM = mainVM.CurrentView;
                        Debug.WriteLine($"PlayerView_Drop: CurrentView type = {currentVM?.GetType().Name ?? "NULL"}");
                        
                        // Delegate to appropriate ViewModel
                        if (currentVM is AudioPlayerViewModel audioVM)
                        {
                            Debug.WriteLine("PlayerView_Drop: Calling AudioPlayerViewModel.LoadAudio");
                            await audioVM.LoadAudio(files[0]);
                            Debug.WriteLine("PlayerView_Drop: AudioPlayerViewModel.LoadAudio completed");
                        }
                        else if (currentVM is VideoPlayerViewModel videoVM)
                        {
                            Debug.WriteLine("PlayerView_Drop: Calling VideoPlayerViewModel.LoadVideo");
                            await videoVM.LoadVideo(files[0]);
                            Debug.WriteLine("PlayerView_Drop: VideoPlayerViewModel.LoadVideo completed");
                        }
                        else
                        {
                            Debug.WriteLine($"PlayerView_Drop: ERROR - Unexpected ViewModel type: {currentVM?.GetType().Name}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("PlayerView_Drop: No files in drop data");
                }
            }
            else
            {
                Debug.WriteLine("PlayerView_Drop: Not FileDrop data");
            }
            
            Debug.WriteLine("PlayerView_Drop: Event handler completed");
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
