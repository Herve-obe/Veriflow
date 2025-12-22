using Avalonia.Controls;
using System.IO;
using System.Reflection;

namespace Veriflow.Avalonia.Views;

public partial class HelpWindow : Window
{
    public string MarkdownContent { get; set; } = string.Empty;

    public HelpWindow()
    {
        InitializeComponent();
        LoadUserGuide();
        DataContext = this;
    }

    private void LoadUserGuide()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Veriflow.Avalonia.Assets.Help.UserGuide.md";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                MarkdownContent = reader.ReadToEnd();
            }
            else
            {
                // Fallback: try to load from file system
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Help", "UserGuide.md");
                if (File.Exists(filePath))
                {
                    MarkdownContent = File.ReadAllText(filePath);
                }
                else
                {
                    MarkdownContent = "# User Guide\n\nUser guide not found.";
                }
            }
        }
        catch (Exception ex)
        {
            MarkdownContent = $"# Error\n\nFailed to load user guide: {ex.Message}";
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
