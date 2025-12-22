using Avalonia.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Veriflow.Avalonia.Services;

/// <summary>
/// Helper class for handling drag-and-drop operations with Avalonia's new APIs
/// Wraps the new DataTransfer APIs to provide a cleaner interface
/// </summary>
public static class DragDropHelper
{
    /// <summary>
    /// Gets file paths from drag event (synchronous version)
    /// </summary>
    public static IEnumerable<string> GetFiles(DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var items = e.Data.GetFiles();
            if (items != null)
            {
                return items.Select(item => item.Path.LocalPath);
            }
        }
        
        return Enumerable.Empty<string>();
    }
    
    /// <summary>
    /// Checks if drag event contains files
    /// </summary>
    public static bool HasFiles(DragEventArgs e)
    {
        return e.Data.Contains(DataFormats.Files);
    }
    
    /// <summary>
    /// Gets the first file path from drag event, or null if none
    /// </summary>
    public static string? GetFirstFile(DragEventArgs e)
    {
        var files = GetFiles(e);
        return files.FirstOrDefault();
    }
}
