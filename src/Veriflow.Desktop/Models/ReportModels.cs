using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Veriflow.Desktop.ViewModels;

namespace Veriflow.Desktop.Models
{
    public enum ReportType
    {
        Video,
        Audio
    }

    public partial class ReportHeader : ObservableObject
    {
        [ObservableProperty] private string _projectName = "";
        [ObservableProperty] private string _reportDate = "";
        
        [ObservableProperty] private DateTime? _calendarDate;

        partial void OnCalendarDateChanged(DateTime? value)
        {
            if (value.HasValue)
            {
                ReportDate = value.Value.ToShortDateString();
            }
        }
        [ObservableProperty] private string _productionCompany = "";
        [ObservableProperty] private string _operatorName = "";
        
        // Context specific
        [ObservableProperty] private string _director = ""; // Video
        [ObservableProperty] private string _dop = "";      // Video
        [ObservableProperty] private string _soundMixer = ""; // Audio
        [ObservableProperty] private string _boomOperator = ""; // Audio
        [ObservableProperty] private string _location = "";     // Audio
        [ObservableProperty] private string _timecodeRate = ""; // Audio
        [ObservableProperty] private string _bitDepth = "";     // Audio
        [ObservableProperty] private string _sampleRate = "";   // Audio
        [ObservableProperty] private string _filesType = "";    // Audio
        
        [ObservableProperty] private string _dataManager = ""; // Video
        [ObservableProperty] private string _cameraId = "";    // Video
        [ObservableProperty] private string _reelName = "";    // Video
        [ObservableProperty] private string _lensInfo = "";    // Video
        
        [ObservableProperty] private string _episode = "";
        [ObservableProperty] private string _scene = "";
        [ObservableProperty] private string _take = "";
        
        [ObservableProperty] private string _globalNotes = "";
    }

    public partial class ReportItem : ObservableObject
    {
        public MediaItemViewModel OriginalMedia { get; }

        public ReportItem(MediaItemViewModel media)
        {
            OriginalMedia = media;
            Filename = media.Name;
            
            // Resolve Timecode
            string? tc = media.CurrentVideoMetadata?.StartTimecode;
            if (string.IsNullOrEmpty(tc)) tc = media.CurrentMetadata?.TimecodeStart;
            Timecode = tc ?? "00:00:00:00";

            Duration = media.Duration;
            Format = System.IO.Path.GetExtension(media.FullName).ToUpperInvariant().Replace(".", "");
            
            // New Fields Population
            // Try to parse Scene/Take from metadata if available (Video usually has this in Name or Metadata)
            Scene = media.CurrentMetadata?.Scene ?? ""; 
            Take = media.CurrentMetadata?.Take ?? ""; 
            
            // Audio Specifics
            // Check based on extension or metadata presence
            bool isVideo = MediaViewModel.VideoExtensions.Contains(System.IO.Path.GetExtension(media.FullName).ToLower());

            if (!isVideo && media.CurrentMetadata != null)
            {
                SampleRate = media.CurrentMetadata.SampleRateString;
                // Build Track Names string "1:MixL 2:MixR"
                if (media.CurrentMetadata.Tracks != null)
                {
                     var trackList = new System.Collections.Generic.List<string>();
                     foreach(var t in media.CurrentMetadata.Tracks)
                     {
                         trackList.Add($"{t.ChannelIndex}:{t.Name}");
                     }
                     TrackNames = string.Join(" ", trackList);
                }
            }
            // Video Specifics
            if (isVideo && media.CurrentVideoMetadata != null)
            {
                 Resolution = media.CurrentVideoMetadata.Resolution; // Use Resolution string directly
                 Codec = media.CurrentVideoMetadata.Codec;
                 ThumbnailPath = media.ThumbnailPath;
            }
        }

        // Display Properties (Snapshot of media state)
        public string Filename { get; }
        public string Timecode { get; }
        public string Duration { get; }
        public string Format { get; }

        // Editable / New Fields
        [ObservableProperty] private string _scene = "";
        [ObservableProperty] private string _take = "";
        [ObservableProperty] private bool _isCircled;
        [ObservableProperty] private string _itemNotes = "";
        
        // Audio Specific
        public string TrackNames { get; set; } = "";
        public string SampleRate { get; set; } = "";
        
        // Video Specific
        public string Resolution { get; set; } = "";
        public string Codec { get; set; } = "";
        public string? ThumbnailPath { get; set; }
        
        // Additional Video Properties
        public string Fps { get; set; } = "";
        public string Iso { get; set; } = "";
        public string WhiteBalance { get; set; } = "";


    }
}
