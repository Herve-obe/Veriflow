using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Windows;

namespace Veriflow.Desktop.Views
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                await WebView.EnsureCoreWebView2Async(null);

                // Get path to HTML file
                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Help", "UserGuide.html");

                if (File.Exists(htmlPath))
                {
                    // Navigate to local HTML file
                    WebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                }
                else
                {
                    // Fallback: Show error message in HTML
                    var errorHtml = @"
                        <!DOCTYPE html>
                        <html>
                        <head>
                            <style>
                                body { 
                                    background: #1E1E1E; 
                                    color: #E0E0E0; 
                                    font-family: 'Segoe UI', sans-serif;
                                    padding: 40px;
                                    text-align: center;
                                }
                                h1 { color: #E64B3D; }
                            </style>
                        </head>
                        <body>
                            <h1>Help Documentation Not Found</h1>
                            <p>The user guide file could not be located.</p>
                            <p>Expected location: Assets/Help/UserGuide.html</p>
                        </body>
                        </html>";
                    
                    WebView.CoreWebView2.NavigateToString(errorHtml);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading help documentation:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
