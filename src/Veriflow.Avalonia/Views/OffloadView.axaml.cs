using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Veriflow.Avalonia.ViewModels;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;

namespace Veriflow.Avalonia.Views;

public partial class OffloadView : UserControl
{
    public OffloadView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PopulateFileTree();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is OffloadViewModel viewModel)
        {
            viewModel.RequestFolderPicker += OnRequestFolderPicker;
        }
    }

    private async void OnRequestFolderPicker(string target)
    {
        if (DataContext is not OffloadViewModel viewModel) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var folder = await Services.FilePickerService.PickFolderAsync(topLevel.StorageProvider);
        
        if (!string.IsNullOrEmpty(folder))
        {
            if (target == "source")
                viewModel.SourcePath = folder;
            else if (target == "dest1")
                viewModel.Destination1Path = folder;
            else if (target == "dest2")
                viewModel.Destination2Path = folder;
        }
    }

    private void PopulateFileTree()
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new TreeViewItem
            {
                Header = $"{d.Name} ({d.VolumeLabel})",
                Tag = d.RootDirectory.FullName,
                Items = new ObservableCollection<TreeViewItem> { new TreeViewItem { Header = "Loading..." } }
            });

        FileTreeView.ItemsSource = drives;
    }

    private void FileTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FileTreeView.SelectedItem is TreeViewItem item && item.Tag is string path)
        {
            // Expand folder on selection
            if (item.Items?.Count == 1 && item.Items[0] is TreeViewItem placeholder && placeholder.Header?.ToString() == "Loading...")
            {
                item.Items.Clear();
                LoadSubFolders(item, path);
            }

            // Set as source path
            if (DataContext is OffloadViewModel viewModel)
            {
                viewModel.SourcePath = path;
            }
        }
    }

    private void LoadSubFolders(TreeViewItem parentItem, string path)
    {
        try
        {
            var directories = Directory.GetDirectories(path);
            foreach (var dir in directories.Take(100)) // Limit to avoid performance issues
            {
                var dirInfo = new DirectoryInfo(dir);
                var childItem = new TreeViewItem
                {
                    Header = dirInfo.Name,
                    Tag = dirInfo.FullName,
                    Items = new ObservableCollection<TreeViewItem> { new TreeViewItem { Header = "Loading..." } }
                };
                parentItem.Items?.Add(childItem);
            }
        }
        catch
        {
            // Ignore access denied errors
        }
    }
}
