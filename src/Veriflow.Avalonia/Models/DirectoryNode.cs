using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Veriflow.Avalonia.Models;

/// <summary>
/// Type of directory node (drive or folder).
/// </summary>
public enum DirectoryNodeType
{
    Folder,
    FixedDrive,      // HDD/SSD
    RemovableDrive,  // USB, SD Card
    NetworkDrive,
    CDRom
}

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

    [ObservableProperty]
    private DirectoryNodeType _nodeType = DirectoryNodeType.Folder;

    /// <summary>
    /// Indicates if this node has a placeholder child (for lazy loading).
    /// </summary>
    public bool HasPlaceholder => Children.Count == 1 && Children[0].Name == "...";

    public bool IsDrive => NodeType != DirectoryNodeType.Folder;
    public bool IsFolder => NodeType == DirectoryNodeType.Folder;

    /// <summary>
    /// Default constructor for object initializer.
    /// </summary>
    public DirectoryNode()
    {
    }

    /// <summary>
    /// Creates a directory node with a placeholder child for lazy loading.
    /// </summary>
    public DirectoryNode(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
        
        // Add placeholder to show expand arrow (only if not a placeholder itself)
        if (!string.IsNullOrEmpty(fullPath))
        {
            Children.Add(CreatePlaceholder());
        }
    }

    /// <summary>
    /// Creates a placeholder node for lazy loading indication.
    /// </summary>
    private static DirectoryNode CreatePlaceholder()
    {
        return new DirectoryNode
        {
            Name = "...",
            FullPath = string.Empty
        };
    }
}
