using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Veriflow.Desktop.Services;
using System.Windows.Input;

namespace Veriflow.Desktop.ViewModels
{
    public partial class MediaViewModel : ObservableObject
    {
        private readonly AudioPreviewService _audioService = new();

        [ObservableProperty]
        private ObservableCollection<DriveViewModel> _drives = new();

        [ObservableProperty]
        private MediaItemViewModel? _selectedMedia;

        partial void OnSelectedMediaChanged(MediaItemViewModel? value)
        {
            value?.LoadMetadata();
        }

        [ObservableProperty]
        private bool _isPreviewing;
        
        [ObservableProperty]
        private string _currentPath = @"C:\";

        [ObservableProperty]
        private ObservableCollection<MediaItemViewModel> _fileList = new();

        private System.Timers.Timer _driveWatcher;

        public MediaViewModel()
        {
            // Initial Drive Load
            RefreshDrives();

            // Setup Drive Polling (Hot-Plug Support)
            _driveWatcher = new System.Timers.Timer(3000); // Check every 3 seconds to be less aggressive
            _driveWatcher.Elapsed += (s, e) => 
            {
                // Dispatch to UI thread if drives changed
                try 
                {
                    // Note: This check is also potentially dangerous if a drive hangs, but we keep it simple for now.
                    // We catch everything just in case.
                    var currentDrives = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name).OrderBy(n => n).ToList();
                    
                    // Accessing Drives here needs thread safety consideration if not on UI thread, 
                    // but for a simple property read it's usually okay. Better to dispatch the check if we were strict.
                    // However, to keep it simple and safe, we will just ALWAYS redispatch the verification or just rely on manual refresh if this is problematic.
                    // Let's do a safe dispatch to check.
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                         var loadedDrives = Drives.Select(d => d.Path).OrderBy(n => n).ToList();
                         if (!currentDrives.SequenceEqual(loadedDrives))
                         {
                             RefreshDrives();
                         }
                    });
                }
                catch {}
            };
            _driveWatcher.Start();
        }

        private void RefreshDrives()
        {
            // We do NOT clear purely blindly to avoid flashing, but for safety against errors, 
            // let's rebuild a temporary list first.
            var safeDriveList = new List<DriveViewModel>();

            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    try 
                    {
                        if (drive.IsReady)
                        {
                            safeDriveList.Add(new DriveViewModel(drive, LoadDirectory));
                        }
                    }
                    catch 
                    { 
                        // Skip individual failing drives (e.g. unformatted partitions, network drives disconnected)
                    }
                }
            }
            catch 
            {
               // This would catch a critical failure in GetDrives() itself
            }

            // Now update the ObservableCollection on the UI thread
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                UpdateDrivesCollection(safeDriveList);
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => UpdateDrivesCollection(safeDriveList));
            }
        }

        private void UpdateDrivesCollection(List<DriveViewModel> newDrives)
        {
            Drives.Clear();
            foreach (var d in newDrives)
            {
                Drives.Add(d);
            }
        }

        private void LoadDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                CurrentPath = path; // Update breadcrumb/path if we had one
                FileList.Clear();
                var dirInfo = new DirectoryInfo(path);

                try
                {
                    // Add supported media files
                    var extensions = new[] { ".wav", ".mp3", ".m4a", ".mov", ".mp4", ".ts", ".mxf", ".avi" };
                    foreach (var file in dirInfo.GetFiles().Where(f => extensions.Contains(f.Extension.ToLower())))
                    {
                        FileList.Add(new MediaItemViewModel(file));
                    }
                }
                catch { /* Access denied or other error */ }
            }
        }

        [RelayCommand]
        private void PreviewFile(MediaItemViewModel item)
        {
             // If clicking the currently playing item, stop it.
             if (SelectedMedia == item && IsPreviewing)
             {
                 StopPreview();
                 return;
             }

             // Stop previous
             if (SelectedMedia != null) SelectedMedia.IsPlaying = false;

             SelectedMedia = item;
             SelectedMedia.IsPlaying = true;
             IsPreviewing = true;
             
             _audioService.Play(item.File.FullName);
        }

        [RelayCommand]
        private void StopPreview()
        {
            _audioService.Stop();
            IsPreviewing = false;
            if (SelectedMedia != null) SelectedMedia.IsPlaying = false;
        }
    }

    public partial class MediaItemViewModel : ObservableObject
    {
        public FileInfo File { get; }
        public string Name => File.Name;
        public string FullName => File.FullName;
        public DateTime CreationTime => File.CreationTime;
        public long Length => File.Length;

        [ObservableProperty]
        private bool _isPlaying;

        // Metadata
        [ObservableProperty] private string _duration = "--:--";
        [ObservableProperty] private string _sampleRate = "";
        [ObservableProperty] private string _channels = "";
        [ObservableProperty] private string _bitDepth = "";
        [ObservableProperty] private string _format = "";
        
        private bool _metadataLoaded;

        public MediaItemViewModel(FileInfo file)
        {
            File = file;
        }

        public void LoadMetadata()
        {
            if (_metadataLoaded) return;
            
            try
            {
                // Only attempt for audio/video files
                var ext = File.Extension.ToLower();
                if (new[] { ".wav", ".mp3", ".m4a", ".aiff", ".wma" }.Contains(ext))
                {
                    using var reader = new NAudio.Wave.AudioFileReader(File.FullName);
                    Duration = reader.TotalTime.ToString(@"mm\:ss");
                    SampleRate = $"{reader.WaveFormat.SampleRate} Hz";
                    Channels = reader.WaveFormat.Channels == 1 ? "Mono" : "Stereo";
                    BitDepth = $"{reader.WaveFormat.BitsPerSample} bit";
                    Format = ext.Substring(1).ToUpper();
                    _metadataLoaded = true;
                }
                // For Video, NAudio MediaFoundationReader might work but is heavier. 
                // We keep it simple for now or try-catch it.
            }
            catch (Exception)
            {
                // Metadata load failed (not a valid audio file or locked)
                Format = "Unknown";
            }
        }
    }

    // Simple ViewModel for TreeView capabilities
    public class DriveViewModel : ObservableObject
    {
        public string Name { get; }
        public string Path { get; }
        public ObservableCollection<FolderViewModel> Folders { get; } = new();
        private readonly Action<string> _onSelect;

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
