using System;
using System.Linq;

namespace Veriflow.Desktop.Services
{
    public static class TimecodeHelper
    {
        /// <summary>
        /// Formats a boolean duration into SMPTE timecode (HH:MM:SS:FF).
        /// </summary>
        /// <param name="time">Absolute time from start of file.</param>
        /// <param name="fps">Frame rate (frames per second).</param>
        /// <param name="startOffset">Optional Start Timecode offset.</param>
        /// <returns>Formatted string "HH:MM:SS:FF"</returns>
        public static string FormatTimecode(TimeSpan time, double fps, TimeSpan startOffset = default)
        {
            if (fps <= 0) fps = 25; // Safe default

            // Add Start Offset
            var absoluteTime = time + startOffset;

            // Calculate components
            double totalSeconds = absoluteTime.TotalSeconds;
            
            // Handle hours > 24 if necessary (usually wraps but simple cast is fine for now)
            int h = (int)absoluteTime.TotalHours; 
            int m = absoluteTime.Minutes;
            int s = absoluteTime.Seconds;
            
            // Calculate frames from the fractional second part to avoid rounding drifts over long durations
            // when converting back and forth.
            // Note: simple (totalSeconds - int seconds) * fps is standard for non-drop display.
            int frames = (int)Math.Round((totalSeconds - (int)totalSeconds) * fps);

            if (frames >= fps) frames = 0; // Wrap safety

            return $"{h:D2}:{m:D2}:{s:D2}:{frames:D2}";
        }

        /// <summary>
        /// Parses a frame rate string into a double (e.g., "24 fps", "23.976", "25,00").
        /// </summary>
        public static double ParseFrameRate(string frameRateString)
        {
            if (string.IsNullOrWhiteSpace(frameRateString)) return 0;

            try
            {
                // Normalize "25,00" to "25.00"
                string normalizedFn = frameRateString.Replace(',', '.');
                
                // Keep only digits and decimal point
                string fpsString = new string(normalizedFn.Where(c => char.IsDigit(c) || c == '.').ToArray());

                if (double.TryParse(fpsString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedFps))
                {
                    return parsedFps;
                }
            }
            catch 
            {
                // Ignore errors
            }

            return 0;
        }

        /// <summary>
        /// Parses a Timecode string "HH:MM:SS:FF" or "HH:MM:SS" into a TimeSpan (Offset).
        /// </summary>
        public static TimeSpan ParseTimecodeOffset(string timecode, double fps)
        {
            if (string.IsNullOrWhiteSpace(timecode)) return TimeSpan.Zero;
            if (fps <= 0) fps = 25;

            try
            {
                var parts = timecode.Split(':');
                if (parts.Length >= 3)
                {
                    int h = int.Parse(parts[0]);
                    int m = int.Parse(parts[1]);
                    int s = int.Parse(parts[2]);

                    double ms = 0;
                    if (parts.Length == 4)
                    {
                        int f = int.Parse(parts[3]);
                        ms = (f / fps) * 1000;
                    }
                    else if (parts.Length == 3 && timecode.Contains(".")) 
                    {
                         // FFprobe might return HH:MM:SS.mmm
                         // But usually we handle SMPTE ':' colon spec here
                    }

                    return new TimeSpan(0, h, m, s, (int)ms);
                }
            }
            catch 
            {
                // Fallback
            }
            return TimeSpan.Zero;
        }
    }
}
