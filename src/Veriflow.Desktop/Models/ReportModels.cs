using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Veriflow.Desktop.ViewModels;
using System.Linq;

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

        // Core Identity
        [ObservableProperty] private string _clipName = "";
        [ObservableProperty] private string _filename = "";
        [ObservableProperty] private string _status = "Ready";

        // Time / Specs
        [ObservableProperty] private string _startTimeCode = "00:00:00:00";
        [ObservableProperty] private string _duration = "--:--";
        [ObservableProperty] private string _fps = "";
        [ObservableProperty] private string _codec = ""; // Also serves as Format
        [ObservableProperty] private string _resolution = "";

        // Editorial (Editable)
        [ObservableProperty] private string _scene = "";
        [ObservableProperty] private string _take = "";
        [ObservableProperty] private bool _isCircled;
        [ObservableProperty] private string _itemNotes = "";

        // Video Specific (Editable)
        [ObservableProperty] private string _iso = "";
        [ObservableProperty] private string _whiteBalance = "";
        public string? ThumbnailPath { get; set; }

        // Audio Specific (Editable)
        [ObservableProperty] private string _tracks = ""; // For track names
        [ObservableProperty] private string _sampleRate = "";

        public ReportItem(MediaItemViewModel media)
        {
            OriginalMedia = media;
            
            // 1. Shared Properties
            ClipName = media.Name;
            Filename = media.Name;
            Duration = media.Duration ?? "--:--"; // Ensure default
            
            // 2. Video vs Audio Logic
            string ext = System.IO.Path.GetExtension(media.FullName).ToLower();
            bool isVideo = MediaViewModel.VideoExtensions.Contains(ext);

            if (isVideo)
            {
                 // Handle Video Metadata
                 var vMeta = media.CurrentVideoMetadata;
                 if (vMeta != null)
                 {
                     Fps = vMeta.FrameRate ?? "--";
                     Resolution = vMeta.Resolution ?? "--";
                     StartTimeCode = vMeta.StartTimecode ?? "00:00:00:00";
                     Codec = !string.IsNullOrEmpty(vMeta.Codec) ? vMeta.Codec : media.Format;
                     // Wait, actually user says "Codec (string) -> Defaults to OriginalMedia.Format." 
                 }
                 else
                 {
                     Fps = "--";
                     Resolution = "--";
                     StartTimeCode = "00:00:00:00";
                     Codec = media.Format ?? "Unknown";
                 }
                 
                 Iso = "";
                 WhiteBalance = "";
                 ThumbnailPath = media.ThumbnailPath;
            }
            else
            {
                // Handle Audio Metadata
                var aMeta = media.CurrentMetadata;
                
                Codec = media.Format; // Generic Format
                
                 if (aMeta != null)
                 {
                      SampleRate = media.SampleRate ?? ""; // User instruction for SampleRate
                      // BitDepth = media.BitDepth; // Skipped as property not added yet
                      StartTimeCode = aMeta.TimecodeStart ?? "00:00:00:00";
                      
                      if (aMeta.Tracks != null)
                      {
                           var trackList = aMeta.Tracks.Select(t => $"{t.ChannelIndex}:{t.Name}");
                           Tracks = string.Join(" ", trackList);
                      }
                      
                      Scene = aMeta.Scene ?? "";
                      Take = aMeta.Take ?? "";
                 }
                 else
                 {
                     StartTimeCode = "00:00:00:00";
                     SampleRate = "--";
                 }
            }

            // Fallback for nulls on properties that must be strings
            if (string.IsNullOrEmpty(StartTimeCode)) StartTimeCode = "00:00:00:00";
            if (Fps == null) Fps = "--";
            if (Resolution == null) Resolution = "--";
            if (Codec == null) Codec = "";
        }
    }
}
