using System.Collections.Generic;

namespace Veriflow.Desktop.Models
{
    public class AudioMetadata
    {
        public string Filename { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty; // e.g. "48kHz / 24bit"
        public string Scene { get; set; } = string.Empty;
        public string Take { get; set; } = string.Empty;
        public string Tape { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string TimecodeStart { get; set; } = string.Empty; // HH:MM:SS:FF
        public string Originator { get; set; } = string.Empty;
        public string CreationDate { get; set; } = string.Empty;
        public List<string> TrackNames { get; set; } = new List<string>();
        public int ChannelCount { get; set; }
    }
}
