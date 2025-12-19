using System;
using System.Collections.Generic;

namespace Veriflow.Core.Models
{
    /// <summary>
    /// Represents a Veriflow session containing the complete application state
    /// </summary>
    public class Session
    {
        public string SessionName { get; set; } = "Untitled Session";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;
        
        // Application State
        public string CurrentMode { get; set; } = "Video"; // "Audio" or "Video"
        public string CurrentPage { get; set; } = "SecureCopy"; // "Media", "Player", "SecureCopy", etc.
        
        // Media Files
        public List<string> MediaFiles { get; set; } = new();
        
        // Reports
        public List<ReportItemData> AudioReportItems { get; set; } = new();
        public List<ReportItemData> VideoReportItems { get; set; } = new();
        
        // SecureCopy Settings
        public SecureCopySettings? SecureCopySettings { get; set; }
        
        // Transcode Queue
        public List<string> TranscodeQueue { get; set; } = new();
    }

    /// <summary>
    /// Simplified report item data for serialization
    /// </summary>
    public class ReportItemData
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Scene { get; set; } = string.Empty;
        public string Take { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Timecode { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public List<ClipData>? Clips { get; set; }
    }

    /// <summary>
    /// Clip data for video reports
    /// </summary>
    public class ClipData
    {
        public string InPoint { get; set; } = string.Empty;
        public string OutPoint { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// SecureCopy configuration
    /// </summary>
    public class SecureCopySettings
    {
        public string SourcePath { get; set; } = string.Empty;
        public string MainDestination { get; set; } = string.Empty;
        public string SecondaryDestination { get; set; } = string.Empty;
    }
}
