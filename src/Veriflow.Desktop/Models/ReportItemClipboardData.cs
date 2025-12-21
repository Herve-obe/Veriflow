using System.Collections.Generic;

namespace Veriflow.Desktop.Models
{
    /// <summary>
    /// Data structure for clipboard serialization of report items
    /// </summary>
    public class ReportItemClipboardData
    {
        public string Version { get; set; } = "1.0";
        public string ReportType { get; set; } = ""; // "Audio" or "Video"
        public List<ReportItemData> Items { get; set; } = new();
    }

    /// <summary>
    /// Serializable representation of a ReportItem
    /// </summary>
    public class ReportItemData
    {
        // File reference
        public string FilePath { get; set; } = "";
        
        // Core Identity
        public string ClipName { get; set; } = "";
        public string Filename { get; set; } = "";
        public string Status { get; set; } = "Ready";
        
        // Time / Specs
        public string StartTimeCode { get; set; } = "00:00:00:00";
        public string Duration { get; set; } = "--:--";
        public string Fps { get; set; } = "";
        public string Codec { get; set; } = "";
        public string Resolution { get; set; } = "";
        
        // Editorial
        public string Scene { get; set; } = "";
        public string Take { get; set; } = "";
        public bool IsCircled { get; set; }
        public string ItemNotes { get; set; } = "";
        
        // Video Specific
        public string Iso { get; set; } = "";
        public string WhiteBalance { get; set; } = "";
        
        // Audio Specific
        public string Tracks { get; set; } = "";
        public string SampleRate { get; set; } = "";
        public string BitDepth { get; set; } = "";
        
        // Clips (for video)
        public List<ClipLogItemData> Clips { get; set; } = new();
    }

    /// <summary>
    /// Serializable representation of a ClipLogItem
    /// </summary>
    public class ClipLogItemData
    {
        public string InPoint { get; set; } = "";
        public string OutPoint { get; set; } = "";
        public string Duration { get; set; } = "";
        public string Notes { get; set; } = "";
    }
}
