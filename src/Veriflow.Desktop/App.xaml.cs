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
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup Crash Logging
        AppDomain.CurrentDomain.UnhandledException += (s, args) => 
            LogCrash((Exception)args.ExceptionObject, "AppDomain.UnhandledException");
        
        DispatcherUnhandledException += (s, args) => 
        {
            LogCrash(args.Exception, "DispatcherUnhandledException");
            args.Handled = true; // Prevent immediate termination if possible, to allow message box
        };

        try
        {
            // Initialize Video Engine (LibVLC) - Single instance
            Services.VideoEngineService.Instance.Initialize();

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
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
