using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace Veriflow.Desktop.ViewModels
{
    // Simple ViewModel for TreeView capabilities
    public class DriveViewModel : ObservableObject
    {
        public string Name { get; }
        public string Path { get; }
        public ObservableCollection<FolderViewModel> Folders { get; } = new();
        private readonly Action<string> _onSelect;

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value) && value)
                {
                    _onSelect?.Invoke(Path);
                }
            }
        }

        public DriveViewModel(DriveInfo drive, Action<string> onSelect)
        {
            // SAFE LABEL ACCESS
            string label = "";
            try 
            { 
                label = drive.VolumeLabel; 
            } 
            catch { } // Ignore failure to get label

            if (string.IsNullOrWhiteSpace(label))
            {
                Name = drive.Name; // Just "C:\"
            }
            else
            {
                Name = $"{label} ({drive.Name})";
            }

            Path = drive.Name;
            _onSelect = onSelect;
            
            // Lazy loading dummy
            Folders.Add(new FolderViewModel("Loading...", "", null!)); 
            LoadFolders();
        }

        private void LoadFolders()
        {
            Folders.Clear();
            try
            {
                foreach (var dir in new DirectoryInfo(Path).GetDirectories())
                {
                    // Basic hidden check
                    if ((dir.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    {
                         Folders.Add(new FolderViewModel(dir.Name, dir.FullName, _onSelect));
                    }
                }
            }
            catch { }
        }
        
    }

    public class FolderViewModel : ObservableObject
    {
        public string Name { get; }
        public string FullPath { get; }
        public ObservableCollection<FolderViewModel> Children { get; } = new();
        private readonly Action<string> _onSelect;
        private bool _isExpanded;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value) && value)
                {
                    LoadChildren();
                }
            }
        }
        
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value) && value)
                {
                    _onSelect?.Invoke(FullPath);
                }
            }
        }

        public FolderViewModel(string name, string fullPath, Action<string> onSelect)
        {
            Name = name;
            FullPath = fullPath;
            _onSelect = onSelect;

            // Add dummy item for lazy loading if it has children
            // Simplified: just always add dummy to show expansion arrow, verify later
             if (!string.IsNullOrEmpty(fullPath)) 
                 Children.Add(new FolderViewModel("...", "", null!));
        }

        private void LoadChildren()
        {
            // Only load if it contains the dummy
            if (Children.Count == 1 && Children[0].Name == "...")
            {
                Children.Clear();
                try
                {
                    var dirInfo = new DirectoryInfo(FullPath);
                    foreach (var dir in dirInfo.GetDirectories())
                    {
                        if ((dir.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                        {
                            Children.Add(new FolderViewModel(dir.Name, dir.FullName, _onSelect));
                        }
                    }
                }
                catch { }
            }
        }
    }
}
