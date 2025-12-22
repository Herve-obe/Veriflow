using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Veriflow.Avalonia.Models;

namespace Veriflow.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the file explorer component.
/// Manages directory tree with lazy loading.
/// </summary>
public partial class FileExplorerViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<DirectoryNode> _rootNodes = new();

    [ObservableProperty]
    private string? _selectedDirectory;

    public FileExplorerViewModel()
    {
        LoadDrives();
    }

    /// <summary>
    /// Loads all logical drives as root nodes.
    /// </summary>
    private void LoadDrives()
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .OrderBy(d => d.Name);

            foreach (var drive in drives)
            {
                var node = new DirectoryNode(
                    name: $"{drive.Name} ({drive.VolumeLabel})",
                    fullPath: drive.RootDirectory.FullName
                );
                
                node.PropertyChanged += Node_PropertyChanged;
                RootNodes.Add(node);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading drives: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles property changes on directory nodes (expansion, selection).
    /// </summary>
    private void Node_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not DirectoryNode node) return;

        if (e.PropertyName == nameof(DirectoryNode.IsExpanded) && node.IsExpanded)
        {
            // Lazy load children when expanded
            if (node.HasPlaceholder)
            {
                LoadChildren(node);
            }
        }
        else if (e.PropertyName == nameof(DirectoryNode.IsSelected) && node.IsSelected)
        {
            // Update selected directory
            SelectedDirectory = node.FullPath;
        }
    }

    /// <summary>
    /// Loads subdirectories for a given node (lazy loading).
    /// </summary>
    private void LoadChildren(DirectoryNode parent)
    {
        try
        {
            // Remove placeholder
            parent.Children.Clear();

            var directory = new DirectoryInfo(parent.FullPath);
            var subdirectories = directory.GetDirectories()
                .OrderBy(d => d.Name);

            foreach (var subdir in subdirectories)
            {
                // Skip hidden and system directories
                if ((subdir.Attributes & FileAttributes.Hidden) != 0 ||
                    (subdir.Attributes & FileAttributes.System) != 0)
                {
                    continue;
                }

                try
                {
                    // Test if we can access this directory
                    _ = subdir.GetDirectories();

                    var childNode = new DirectoryNode(subdir.Name, subdir.FullName);
                    childNode.PropertyChanged += Node_PropertyChanged;
                    parent.Children.Add(childNode);
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip inaccessible directories silently
                    continue;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error accessing {subdir.FullName}: {ex.Message}");
                    continue;
                }
            }

            // If no children, add a placeholder to indicate empty folder
            if (parent.Children.Count == 0)
            {
                parent.Children.Add(new DirectoryNode("(Empty)", string.Empty));
            }
        }
        catch (UnauthorizedAccessException)
        {
            parent.Children.Clear();
            parent.Children.Add(new DirectoryNode("(Access Denied)", string.Empty));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading children for {parent.FullPath}: {ex.Message}");
            parent.Children.Clear();
            parent.Children.Add(new DirectoryNode("(Error)", string.Empty));
        }
    }
}
