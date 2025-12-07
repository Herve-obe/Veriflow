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

        try
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Une erreur fatale est survenue au démarrage :\n\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace}", 
                            "Erreur de Démarrage", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
            Shutdown();
        }
    }
}

