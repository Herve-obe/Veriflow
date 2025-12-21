using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Veriflow.Desktop.Views
{
    public partial class SplashWindow : Window
    {
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public SplashWindow()
        {
            InitializeComponent();
            LoadHighResIcon();
        }

        private void LoadHighResIcon()
        {
            try
            {
                // Force load the largest frame from the ICO (256x256)
                var iconUri = new Uri("pack://application:,,,/Assets/veriflow.ico");
                var decoder = new IconBitmapDecoder(iconUri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                
                // Select best frame (largest pixel width)
                var bestFrame = decoder.Frames.OrderByDescending(f => f.PixelWidth).FirstOrDefault();
                
                if (bestFrame != null)
                {
                    LogoImage.Source = bestFrame;
                }
            }
            catch (Exception ex)
            {
                // Fallback (though this shouldn't fail if asset exists)
                System.Diagnostics.Debug.WriteLine($"Failed to load high-res icon: {ex.Message}");
            }
        }

        public void UpdateProgress(double value, string message)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingBar.Value = value;
                StatusText.Text = message;
            });
        }
    }
}
