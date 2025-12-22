using System;
using System.IO;
using System.Text.Json;

namespace Veriflow.Avalonia.Services;

public class WindowStateService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Veriflow",
        "window-state.json"
    );

    public class WindowState
    {
        public double Width { get; set; } = 1280;
        public double Height { get; set; } = 720;
        public double X { get; set; } = 100;
        public double Y { get; set; } = 100;
        public bool IsMaximized { get; set; } = true; // Maximized by default on first launch
    }

    public static WindowState Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<WindowState>(json) ?? new WindowState();
            }
        }
        catch
        {
            // If any error, return default
        }

        return new WindowState(); // First launch - maximized by default
    }

    public static void Save(WindowState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
