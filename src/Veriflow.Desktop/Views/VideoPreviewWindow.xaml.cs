using System.Windows;
using System.Windows.Input;
using System.Linq;
using LibVLCSharp.Shared;

namespace Veriflow.Desktop.Views
{
    public partial class VideoPreviewWindow : Window
    {
        private readonly int _videoWidth;
        private readonly int _videoHeight;

        public VideoPreviewWindow(int videoWidth = 1920, int videoHeight = 1080)
        {
            InitializeComponent();
            _videoWidth = videoWidth > 0 ? videoWidth : 1920;
            _videoHeight = videoHeight > 0 ? videoHeight : 1080;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow window dragging by clicking anywhere on the title bar
            try
            {
                DragMove();
            }
            catch
            {
                // DragMove can throw if window state changes during drag
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Calculate window size based on video aspect ratio
            double aspectRatio = (double)_videoWidth / _videoHeight;
            
            // Max constraints to prevent oversized windows
            double maxWidth = 1280;
            double maxHeight = 720;
            
            // Check screen size and adjust max constraints if needed
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            
            // Use 80% of screen size as absolute maximum
            maxWidth = Math.Min(maxWidth, screenWidth * 0.8);
            maxHeight = Math.Min(maxHeight, screenHeight * 0.8);
            
            double targetWidth = _videoWidth;
            double targetHeight = _videoHeight;
            
            // Scale down if video is too large
            if (_videoWidth > maxWidth || _videoHeight > maxHeight)
            {
                if (aspectRatio > (maxWidth / maxHeight))
                {
                    // Width is the limiting factor
                    targetWidth = maxWidth;
                    targetHeight = maxWidth / aspectRatio;
                }
                else
                {
                    // Height is the limiting factor
                    targetHeight = maxHeight;
                    targetWidth = maxHeight * aspectRatio;
                }
            }
            
            // Set window size (add title bar height of 35px + border 2px)
            Width = targetWidth + 2;
            Height = targetHeight + 35 + 2;
            
            // Center window on screen
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = (screenWidth - Width) / 2;
            Top = (screenHeight - Height) / 2;
        }
    }
}
