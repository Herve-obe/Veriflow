using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Veriflow.Avalonia.Models;

/// <summary>
/// Represents a directory node in the file explorer tree.
/// Supports lazy loading of subdirectories.
/// </summary>
public partial class DirectoryNode : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private ObservableCollection<DirectoryNode> _children = new();

    /// <summary>
    /// Indicates if this node has a placeholder child (for lazy loading).
    /// </summary>
    public bool HasPlaceholder => Children.Count == 1 && Children[0].Name == "...";

    /// <summary>
    /// Creates a directory node with a placeholder child for lazy loading.
    /// </summary>
    public DirectoryNode(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
        
        // Add placeholder to show expand arrow
        Children.Add(new DirectoryNode("...", string.Empty));
    }

    /// <summary>
    /// Creates an empty node (used for placeholders).
    /// </summary>
    private DirectoryNode(string name)
    {
        Name = name;
        FullPath = string.Empty;
    }
}
