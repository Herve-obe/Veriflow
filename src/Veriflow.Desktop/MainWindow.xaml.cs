using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Veriflow.Desktop.ViewModels;

namespace Veriflow.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(Models.AppSettings settings)
    {
        InitializeComponent();
        
        // Restore window state from settings
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }
        
        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
        
        DataContext = new MainViewModel();
        
        // Load icon programmatically to prevent random taskbar icon issues
        LoadApplicationIcon();
        
        // Subscribe to window-level keyboard events for persistent shortcuts
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    /// <summary>
    /// Loads the application icon programmatically from embedded resources.
    /// This prevents the random generic icon issue in Windows taskbar that can occur with pack URI loading.
    /// </summary>
    private void LoadApplicationIcon()
    {
        try
        {
            var iconUri = new Uri("pack://application:,,,/Assets/veriflow.ico");
            var streamInfo = Application.GetResourceStream(iconUri);
            
            if (streamInfo != null)
            {
                Icon = BitmapFrame.Create(streamInfo.Stream);
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't crash the application
            System.Diagnostics.Debug.WriteLine($"Failed to load application icon: {ex.Message}");
        }
    }

    // ============================================================================
    // WINDOW-LEVEL KEYBOARD SHORTCUTS (Persistent, Professional-Grade)
    // ============================================================================
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Don't intercept if user is typing in a TextBox
        if (e.OriginalSource is System.Windows.Controls.TextBox || 
            e.OriginalSource is System.Windows.Controls.PasswordBox)
        {
            return;
        }

        if (DataContext is not MainViewModel mainVM) return;

        // Route shortcuts based on active page
        switch (mainVM.CurrentPageType)
        {
            case PageType.Media:
                HandleMediaShortcuts(e, mainVM.MediaViewModel);
                break;

            case PageType.Player:
                // Route to Audio or Video player based on mode
                if (mainVM.CurrentAppMode == AppMode.Audio)
                {
                    HandleAudioPlayerShortcuts(e, mainVM.AudioViewModel);
                }
                else
                {
                    HandleVideoPlayerShortcuts(e, mainVM.VideoPlayerViewModel);
                }
                break;

            // Other pages don't have shortcuts yet
            default:
                break;
        }
    }

    private void HandleMediaShortcuts(KeyEventArgs e, MediaViewModel vm)
    {
        switch (e.Key)
        {
            case Key.Space:
                vm.ToggleFilmstripPlaybackCommand?.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void HandleAudioPlayerShortcuts(KeyEventArgs e, AudioPlayerViewModel vm)
    {
        switch (e.Key)
        {
            case Key.Space:
                vm.TogglePlayPauseCommand?.Execute(null);
                e.Handled = true;
                break;

            case Key.Enter:
                vm.StopCommand?.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void HandleVideoPlayerShortcuts(KeyEventArgs e, VideoPlayerViewModel vm)
    {
        switch (e.Key)
        {
            case Key.Space:
                vm.TogglePlayPauseCommand?.Execute(null);
                e.Handled = true;
                break;

            case Key.Enter:
                vm.StopCommand?.Execute(null);
                e.Handled = true;
                break;

            case Key.I:
                vm.SetInCommand?.Execute(null);
                e.Handled = true;
                break;

            case Key.O:
                vm.SetOutCommand?.Execute(null);
                e.Handled = true;
                break;

            case Key.T:
                vm.TagClipCommand?.Execute(null);
                e.Handled = true;
                break;

            case Key.Left:
                vm.PreviousFrameCommand?.Execute(null);
                e.Handled = true;
                break;

            case Key.Right:
                vm.NextFrameCommand?.Execute(null);
                e.Handled = true;
                break;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        
        // Save window state before closing
        try
        {
            var settings = Services.SettingsService.Instance.GetSettings();
            
            // Save actual window dimensions (not maximized dimensions)
            if (WindowState == WindowState.Normal)
            {
                settings.WindowWidth = ActualWidth;
                settings.WindowHeight = ActualHeight;
            }
            
            settings.WindowMaximized = WindowState == WindowState.Maximized;
            
            Services.SettingsService.Instance.SaveSettings(settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save window state: {ex.Message}");
            // Don't block closing if save fails
        }
        
        // Dispose MainViewModel and all its child ViewModels
        // This stops all timers and releases media resources
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}