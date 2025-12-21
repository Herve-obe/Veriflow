using System;
using System.Configuration;
using System.Data;
using System.Windows;

namespace Veriflow.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup Crash Logging
        AppDomain.CurrentDomain.UnhandledException += (s, args) => 
            LogCrash((Exception)args.ExceptionObject, "AppDomain.UnhandledException");
        
        DispatcherUnhandledException += (s, args) => 
        {
            LogCrash(args.Exception, "DispatcherUnhandledException");
            args.Handled = true; 
        };

        // 1. Show Splash Screen
        var splash = new Views.SplashWindow();
        splash.Show();

        try
        {
            // 2. Perform Background Initialization
            await System.Threading.Tasks.Task.Run(async () =>
            {
                // Step 1: Core Services
                splash.UpdateProgress(20, "Initializing Core Services...");
                await System.Threading.Tasks.Task.Delay(500); // Simulate/Wait

                // Step 2: Audio Engine
                splash.UpdateProgress(40, "Loading Video Engine...");
                Services.VideoEngineService.Instance.Initialize();
                await System.Threading.Tasks.Task.Delay(300);

                // Step 3: FFmpeg Check
                splash.UpdateProgress(60, "Checking Dependencies...");
                string ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                if (!System.IO.File.Exists(ffmpegPath))
                {
                    // Non-blocking warning (logged)
                    System.Diagnostics.Debug.WriteLine("Warning: FFmpeg not found.");
                }
                await System.Threading.Tasks.Task.Delay(300);

                // Step 4: Settings
                splash.UpdateProgress(80, "Loading User Settings...");
                // Load application settings
                var settings = Services.SettingsService.Instance.GetSettings();
                
                // Step 5: Finalize
                splash.UpdateProgress(100, "Ready!");
                await System.Threading.Tasks.Task.Delay(500); // Let user see "Ready"

                // 3. Switch to Main Window
                await Dispatcher.InvokeAsync(() =>
                {
                    var mainWindow = new MainWindow(settings);
                    this.MainWindow = mainWindow; // Set as global MainWindow
                    mainWindow.Show();
                    
                    // Close Splash
                    splash.Close();
                });
            });
        }
        catch (Exception ex)
        {
            splash.Close();
            LogCrash(ex, "StartupException");
            Shutdown();
        }
    }

    private void LogCrash(Exception ex, string source)
    {
        try 
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                "Veriflow_CrashLog.txt");
            
            string message = $"[{DateTime.Now}] CRASH REPORT ({source})\n" +
                             $"Message: {ex.Message}\n" +
                             $"Source: {ex.Source}\n" +
                             $"Stack Trace:\n{ex.StackTrace}\n";

            if (ex.InnerException != null)
            {
                message += $"\nInner Exception:\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n";
            }
            
            message += "--------------------------------------------------\n";

            System.IO.File.AppendAllText(path, message);
            
            MessageBox.Show($"Une erreur est survenue ! \nUn rapport a été généré sur votre bureau : Veriflow_CrashLog.txt\n\nErreur: {ex.Message}", 
                            "Veriflow - Plantage Détecté", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
        }
        catch { 
            // Fallback if logging fails
            MessageBox.Show($"Crash Logging Failed. Original Error: {ex.Message}", "Fatal Error");
        }
    }
}
