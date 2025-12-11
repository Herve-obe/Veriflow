using System;
using System.Collections.Generic;

namespace Veriflow.Desktop.Models
{
    public class VideoMetadata
    {
        public string Filename { get; set; } = "";
        
        // General
        public string Container { get; set; } = ""; // .mov, .mxf
        public string Duration { get; set; } = "--:--:--:--";
        public string Size { get; set; } = ""; // File size
        
        // Video
        public string Codec { get; set; } = ""; // ProRes 422 HQ, DNxHR
        public string Resolution { get; set; } = ""; // 3840x2160
        public string AspectRatio { get; set; } = ""; // 16:9
        public string FrameRate { get; set; } = ""; // 24.00 fps
        public string BitDepth { get; set; } = ""; // 10-bit
        public string ChromaSubsampling { get; set; } = ""; // 4:2:2
        public string ColorSpace { get; set; } = ""; // Rec.709, Rec.2020
        public string ScanType { get; set; } = ""; // Progressive / Interlaced
        public string Bitrate { get; set; } = ""; // 800 Mbps
        public string GopStructure { get; set; } = ""; // Intra / LongGOP

        // Timecode
        public string StartTimecode { get; set; } = "00:00:00:00";
        public string EndTimecode { get; set; } = "00:00:00:00";
        
        // Audio in Video
        public string AudioFormat { get; set; } = ""; // PCM 24-bit 48kHz
        public string AudioChannels { get; set; } = ""; // 8 Channels
        
        public VideoMetadata() { }
    }
}
