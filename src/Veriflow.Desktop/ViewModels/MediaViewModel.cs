using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using CommunityToolkit.Mvvm.Input;
using CSCore;
using CSCore.Codecs;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Veriflow.Desktop.Services;
using System.Windows.Input;
using Veriflow.Desktop.Models;
using System.Windows;
using Veriflow.Desktop.Views;

namespace Veriflow.Desktop.ViewModels
{
    public partial class MediaViewModel : ObservableObject
    {
        private readonly AudioPreviewService _audioService = new();

        [ObservableProperty]
        private ObservableCollection<DriveViewModel> _drives = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OpenInPlayerCommand))]
        [NotifyCanExecuteChangedFor(nameof(SendFileToTranscodeCommand))]
        private MediaItemViewModel? _selectedMedia;

        partial void OnSelectedMediaChanged(MediaItemViewModel? value)
        {
            value?.LoadMetadata();
        }

        [ObservableProperty]
        private bool _isPreviewing;
        
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendToSecureCopyCommand))]
        [NotifyCanExecuteChangedFor(nameof(SendToTranscodeCommand))]
        private string _currentPath = @"C:\";

        public event Action<string>? RequestOffloadSource;
        public event Action<IEnumerable<string>>? RequestTranscode;

        [ObservableProperty]
        private bool _isVideoMode;

        public void SetAppMode(AppMode mode)
        {
            IsVideoMode = (mode == AppMode.Video);
            // Tree structure is identical, do not reset drives.
        }


        private bool CanSendToSecureCopy() => !string.IsNullOrWhiteSpace(CurrentPath) && Directory.Exists(CurrentPath);
        private bool CanSendToTranscode() => !string.IsNullOrWhiteSpace(CurrentPath) && Directory.Exists(CurrentPath) && FileList.Any();

        [RelayCommand(CanExecute = nameof(CanSendToSecureCopy))]
        private void SendToSecureCopy()
        {
             RequestOffloadSource?.Invoke(CurrentPath);
        }

        private bool CanSendFileToTranscode() => SelectedMedia != null;

        [RelayCommand(CanExecute = nameof(CanSendFileToTranscode))]
        private void SendFileToTranscode()
        {
            if (SelectedMedia != null)
                RequestTranscode?.Invoke(new List<string> { SelectedMedia.FullName });
        }

        [RelayCommand(CanExecute = nameof(CanSendToTranscode))]
        private void SendToTranscode()
        {
            var files = FileList.Select(x => x.FullName).ToList();
            if (files.Any())
                RequestTranscode?.Invoke(files);
        }

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

            FileList.CollectionChanged += (s, e) => SendToTranscodeCommand.NotifyCanExecuteChanged();
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

        public static readonly string[] AudioExtensions = { ".wav", ".mp3", ".m4a", ".aac", ".aiff", ".aif", ".flac", ".ogg", ".opus", ".ac3" };
        public static readonly string[] VideoExtensions = { ".mov", ".mp4", ".ts", ".mxf", ".avi", ".mkv", ".webm", ".wmv", ".flv", ".m4v", ".mpg", ".mpeg", ".3gp", ".dv", ".ogv", ".m2v", ".vob", ".m2ts" };

        private void LoadDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                CurrentPath = path; // Update breadcrumb/path if we had one
                FileList.Clear();
                var dirInfo = new DirectoryInfo(path);

                try
                {
                    // Filter based on Mode
                    var targetExtensions = IsVideoMode ? VideoExtensions : AudioExtensions;
                    
                    var files = dirInfo.GetFiles()
                                       .Where(f => targetExtensions.Contains(f.Extension.ToLower()))
                                       .OrderBy(f => f.Name);

                    foreach (var file in files)
                    {
                        FileList.Add(new MediaItemViewModel(file));
                    }
                }
                catch { /* Access denied or other error */ }
            }
        }

        partial void OnIsVideoModeChanged(bool value)
        {
            // Stop any playing preview when switching modes to avoid phantom audio
            StopPreview();
            
            // Reload current directory applying new filter
            if (!string.IsNullOrEmpty(CurrentPath))
            {
                LoadDirectory(CurrentPath);
            }
        }

        [ObservableProperty]
        private bool _isVideoPlaying;

        public MediaPlayer? PreviewPlayer { get; private set; }
        private VideoPreviewWindow? _previewWindow;

        private void InitializePreviewPlayer()
        {
            if (PreviewPlayer == null)
            {
                // Ensure Core is initialized
                VideoEngineService.Instance.Initialize();
                if (VideoEngineService.Instance.LibVLC != null)
                {
                    PreviewPlayer = new MediaPlayer(VideoEngineService.Instance.LibVLC);
                }
            }
        }

        [RelayCommand]
        private void PreviewFile(MediaItemViewModel item)
        {
             // If clicking the currently playing item, stop it.
             if (SelectedMedia == item && (IsPreviewing || IsVideoPlaying))
             {
                 StopPreview();
                 return;
             }

             // Stop previous
             if (SelectedMedia != null) SelectedMedia.IsPlaying = false;

             SelectedMedia = item;
             SelectedMedia.IsPlaying = true;

             if (IsVideoMode)
             {
                 // Video Preview Logic (Floating Window)
                 InitializePreviewPlayer();
                 if (PreviewPlayer != null && VideoEngineService.Instance.LibVLC != null)
                 {
                     IsVideoPlaying = true;
                     using var media = new Media(VideoEngineService.Instance.LibVLC, item.FullName, FromType.FromPath);
                     media.AddOption(":avcodec-hw=d3d11va"); 
                     PreviewPlayer.Play(media);

                     // Manage Window
                     if (_previewWindow == null || !_previewWindow.IsLoaded)
                     {
                         _previewWindow = new VideoPreviewWindow();
                         _previewWindow.DataContext = this; // Bind to VM for Player property
                         _previewWindow.Closed += (s, e) => StopPreview(); // Handle manual close
                         _previewWindow.Show();
                     }
                     
                     if (_previewWindow.WindowState == WindowState.Minimized)
                        _previewWindow.WindowState = WindowState.Normal;
                        
                     _previewWindow.Activate();
                 }
             }
             else
             {
                 // Audio Preview Logic
                 IsPreviewing = true;
                 _audioService.Play(item.File.FullName);
             }
        }

        [RelayCommand]
        private void StopPreview()
        {
            if (IsVideoMode)
            {
                IsVideoPlaying = false;
                if (PreviewPlayer != null && PreviewPlayer.IsPlaying)
                {
                    PreviewPlayer.Stop();
                }

                // Close Window
                if (_previewWindow != null)
                {
                    // Remove Event Handler to prevent recursive loop if called from Closed event
                    _previewWindow.Closed -= (s, e) => StopPreview(); 
                    _previewWindow.Close();
                    _previewWindow = null;
                }
            }
            else
            {
                _audioService.Stop();
                IsPreviewing = false;
            }
            
            if (SelectedMedia != null) SelectedMedia.IsPlaying = false;
        }

        private bool CanUnloadMedia() => SelectedMedia != null;

        [RelayCommand(CanExecute = nameof(CanUnloadMedia))]
        private void UnloadMedia()
        {
            StopPreview();
            SelectedMedia = null;
        }

        public event Action<string>? RequestOpenInPlayer;

        private bool CanOpenInPlayer() => SelectedMedia != null;

        [RelayCommand(CanExecute = nameof(CanOpenInPlayer))]
        private void OpenInPlayer()
        {
            if (SelectedMedia != null)
            {
                // Stop preview if running
                StopPreview();
                RequestOpenInPlayer?.Invoke(SelectedMedia.FullName);
            }
        }

        [RelayCommand]
        private Task DropMedia(DragEventArgs e) => HandleDrop(e);

        [RelayCommand]
        private Task DropExplorer(DragEventArgs e) => HandleDrop(e);

        private async Task HandleDrop(DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null && files.Length > 0)
                    {
                        string dropPath = files[0];
                        string directoryToLoad = dropPath;
                        string? fileToSelect = null;

                        if (File.Exists(dropPath))
                        {
                            // It's a file, get directory
                            directoryToLoad = Path.GetDirectoryName(dropPath) ?? dropPath;
                            fileToSelect = dropPath;
                        }
                        
                        if (Directory.Exists(directoryToLoad))
                        {
                            // Try to navigate via Tree first
                            bool navigated = await ExpandAndSelectPath(directoryToLoad);
                            
                            // If tree didn't work (e.g. drive hidden) OR if it worked but we are not sure if it refreshed 
                            // (IsSelected might have been true already), check consistency.
                            // Assuming LoadDirectory sets CurrentPath.
                            if (!navigated || CurrentPath != directoryToLoad)
                            {
                                LoadDirectory(directoryToLoad);
                            }

                            // If a specific file was dropped, select it to show metadata
                            if (fileToSelect != null)
                            {
                                var mediaItem = FileList.FirstOrDefault(f => f.FullName.Equals(fileToSelect, StringComparison.OrdinalIgnoreCase));
                                if (mediaItem != null)
                                {
                                    SelectedMedia = mediaItem;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Prevent crash from async void/task exceptions
                System.Diagnostics.Debug.WriteLine($"Drag Drop Error: {ex.Message}");
            }
        }

        private async Task<bool> ExpandAndSelectPath(string path)
        {
             // 1. Find Drive
             var drive = Drives.FirstOrDefault(d => path.StartsWith(d.Path, StringComparison.OrdinalIgnoreCase));
             if (drive == null) return false;

             drive.IsExpanded = true;
             await Task.Delay(100); // Give UI time to bind/render if needed, though viewmodels are immediate.

             // 2. Traverse
             string currentPath = drive.Path;
             // Ensure trailing slash for root match cleanliness
             if (!currentPath.EndsWith(Path.DirectorySeparatorChar.ToString())) 
                 currentPath += Path.DirectorySeparatorChar;

             // Split path into segments
             var targetParts = path.Substring(drive.Path.Length).Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
             
             ObservableCollection<FolderViewModel> currentCollection = drive.Folders;
             object currentItem = drive;

             foreach (var part in targetParts)
             {
                 var folder = currentCollection.FirstOrDefault(f => f.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                 if (folder == null) return false; // Not found

                 folder.IsExpanded = true;
                 
                 // Since LoadChildren is synchronous but triggered by setter, it should populate immediately.
                 // However, since it involves IO, we might ideally want to delay or yield slightly if UI is sluggish,
                 // but functionally it's fine.
                 
                 currentCollection = folder.Children;
                 currentItem = folder;
             }
             
             // 3. Select final item


             if (currentItem is FolderViewModel finalFolder)
             {
                 if (!finalFolder.IsSelected)
                 {
                      finalFolder.IsSelected = true;
                 }
             }
             else if (currentItem is DriveViewModel finalDrive)
             {
                 if (!finalDrive.IsSelected)
                 {
                      finalDrive.IsSelected = true;
                 }
             }
             
             return true; 
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

        [ObservableProperty]
        private AudioMetadata _currentMetadata = new();

        [ObservableProperty]
        private VideoMetadata _currentVideoMetadata = new();
        
        [ObservableProperty] private string _duration = "--:--";
        [ObservableProperty] private string _sampleRate = "";
        [ObservableProperty] private string _channels = "";
        [ObservableProperty] private string _bitDepth = "";
        [ObservableProperty] private string _format = "";
        
        [ObservableProperty] private string? _thumbnailPath; // Null by default, indicating no thumbnail yet

        private bool _metadataLoaded;

        public MediaItemViewModel(FileInfo file)
        {
            File = file;

            // Trigger thumbnail generation immediately for videos
            string ext = File.Extension.ToLower();
            if (MediaViewModel.VideoExtensions.Contains(ext))
            {
                TriggerThumbnailLoad();
            }
        }

        private void TriggerThumbnailLoad()
        {
             _ = Task.Run(async () => 
            {
                try
                {
                    var thumbService = new ThumbnailService(); // Cheap allocation
                    string? thumb = await thumbService.GetThumbnailAsync(File.FullName);
                    if (thumb != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MediaVM] Generated: {thumb}");
                        System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        {
                            ThumbnailPath = thumb;
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaVM] Background Error: {ex}");
                }
            });
        }

        public async void LoadMetadata()
        {
            if (_metadataLoaded) return;
            
            try
            {
                var provider = new FFprobeMetadataProvider();
                string ext = File.Extension.ToLower();
                bool isVideo = MediaViewModel.VideoExtensions.Contains(ext);

                if (isVideo)
                {
                    CurrentVideoMetadata = await provider.GetVideoMetadataAsync(File.FullName);
                    if (CurrentVideoMetadata != null)
                    {
                        Duration = CurrentVideoMetadata.Duration;
                        Format = CurrentVideoMetadata.Resolution; 
                    }
                    // Thumbnail generation is now handled in Constructor
                }
                else
                {
                    CurrentMetadata = await provider.GetMetadataAsync(File.FullName);
                    
                    // Populate simple properties from the rich metadata for consistency
                    if (CurrentMetadata != null)
                    {
                        Duration = CurrentMetadata.Duration;
                        Format = CurrentMetadata.Format;
                        
                        var formatParts = CurrentMetadata.Format.Split('/');
                        if (formatParts.Length > 0) SampleRate = formatParts[0].Trim();
                        if (formatParts.Length > 1) BitDepth = formatParts[1].Trim();
                        
                        Channels = CurrentMetadata.ChannelCount.ToString(); 
                        if (CurrentMetadata.ChannelCount == 1) Channels = "Mono";
                        else if (CurrentMetadata.ChannelCount == 2) Channels = "Stereo";
                        else Channels = $"{CurrentMetadata.ChannelCount} Ch";
                    }
                }

                _metadataLoaded = true;
            }
            catch (Exception)
            {
                // Metadata load failed
                Format = "Unknown";
                CurrentMetadata = new AudioMetadata { Filename = File.Name };
                CurrentVideoMetadata = new VideoMetadata { Filename = File.Name };
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
