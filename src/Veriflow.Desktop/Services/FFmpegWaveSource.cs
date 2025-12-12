using CSCore;
using System;
using System.Diagnostics;
using System.IO;

namespace Veriflow.Desktop.Services
{
    /// <summary>
    /// Decodes audio file using FFmpeg and pipes it as standard PCM (s16le).
    /// Allows playback of formats not supported natively by Windows/CSCore (Opus, Vorbis, AC3, etc).
    /// </summary>
    public class FFmpegWaveSource : IWaveSource
    {
        private Process? _ffmpegProcess;
        private Stream? _ffmpegStream;
        private readonly WaveFormat _waveFormat;
        private readonly object _lockObj = new object();

        public FFmpegWaveSource(string filename)
        {
            // Forces 48kHz Stereo 16-bit for universal compatibility
            _waveFormat = new WaveFormat(48000, 16, 2);
            StartFFmpeg(filename);
        }

        private void StartFFmpeg(string filename)
        {
            string ffmpegPath = GetToolPath("ffmpeg");
            // -v error: quiet
            // -i: input
            // -f s16le: Force raw signed 16-bit little endian PCM
            // -ac 2: Force Stereo
            // -ar 48000: Force 48kHz
            // -: Output to stdout
            string args = $"-v error -i \"{filename}\" -f s16le -ac 2 -ar 48000 -";

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            _ffmpegProcess = new Process { StartInfo = startInfo };
            _ffmpegProcess.Start();
            _ffmpegStream = _ffmpegProcess.StandardOutput.BaseStream;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            lock (_lockObj)
            {
                if (_ffmpegStream == null) return 0;
                
                // Read from pipe
                // Note: Pipe reads can block or return fewer bytes.
                // CSCore expects us to try and fill 'count' if possible, or return what we got.
                return _ffmpegStream.Read(buffer, offset, count);
            }
        }

        public WaveFormat WaveFormat => _waveFormat;

        // Piping does not support Seeking easily without restarting logic
        public bool CanSeek => false;

        public long Position
        {
            get => 0; 
            set => throw new NotSupportedException("Seeking not supported in FFmpeg Pipe mode");
        }

        public long Length => 0; // Unknown in a pipe

        public void Dispose()
        {
            lock (_lockObj)
            {
                if (_ffmpegProcess != null)
                {
                    try
                    {
                        if (!_ffmpegProcess.HasExited) _ffmpegProcess.Kill();
                    }
                    catch { }
                    _ffmpegProcess.Dispose();
                    _ffmpegProcess = null;
                }
                _ffmpegStream = null;
            }
            GC.SuppressFinalize(this);
        }

        private string GetToolPath(string toolName)
        {
            if (!toolName.EndsWith(".exe")) toolName += ".exe";
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            var path = Path.Combine(baseDir, toolName);
            if (File.Exists(path)) return path;

            path = Path.Combine(baseDir, "ExternalTools", toolName);
            if (File.Exists(path)) return path;

            return toolName; // Path fallback
        }
    }
}
