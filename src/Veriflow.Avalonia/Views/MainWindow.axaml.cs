using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Veriflow.Avalonia.ViewModels;
using Veriflow.Avalonia.Services;

namespace Veriflow.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        
        // Load window state
        LoadWindowState();
        
        // Save window state on closing
        Closing += OnClosing;
        
        // Setup keyboard shortcuts
        KeyDown += OnKeyDown;
    }

    private void LoadWindowState()
    {
        var state = WindowStateService.Load();
        
        if (state.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
        else
        {
            Width = state.Width;
            Height = state.Height;
            Position = new PixelPoint((int)state.X, (int)state.Y);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        var state = new WindowStateService.WindowState
        {
            IsMaximized = WindowState == WindowState.Maximized,
            Width = Width,
            Height = Height,
            X = Position.X,
            Y = Position.Y
        };
        
        WindowStateService.Save(state);
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
        
        // Update button icon geometry
        if (this.FindControl<Path>("MaximizeIcon") is Path icon)
        {
            // Maximized: show restore icon (two overlapping squares)
            // Normal: show maximize icon (single square)
            icon.Data = WindowState == WindowState.Maximized
                ? Geometry.Parse("M 0,2 L 8,2 L 8,10 L 0,10 Z M 2,0 L 10,0 L 10,8 L 8,8")
                : Geometry.Parse("M 0,0 L 10,0 L 10,10 L 0,10 Z");
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        switch (e.Key)
        {
            case Key.F1:
                vm.NavigateToOffloadCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F2:
                vm.NavigateToMediaCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F3:
                vm.NavigateToPlayerCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F4:
                vm.NavigateToSyncCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F5:
                vm.NavigateToTranscodeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F6:
                vm.NavigateToReportsCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
