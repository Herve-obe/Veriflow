using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;

namespace Veriflow.Desktop.Services
{
    public class AudioPreviewService : IDisposable
    {
        private IWavePlayer? _outputDevice;
        private AudioFileReader? _audioFile;


        public void Play(string filePath)
        {
            Stop(); // Ensure any previous playback is stopped

            try
            {
                if (!File.Exists(filePath)) return;

                _audioFile = new AudioFileReader(filePath);
                
                ISampleProvider sampleProvider = _audioFile;

                // Auto Downmix to Mono for Preview (Listen on both speakers)
                if (_audioFile.WaveFormat.Channels > 1)
                {
                    // Mix all channels to mono 
                    // (Simple average helps avoiding clipping but reduces volume, 
                    // but for preview it's safer to just take L+R/2 or similar)
                    var updated = new StereoToMonoSampleProvider(_audioFile) { LeftVolume = 1.0f, RightVolume = 1.0f };
                    sampleProvider = updated;
                }

                _outputDevice = new WaveOutEvent();
                _outputDevice.Init(sampleProvider);
                _outputDevice.Play();

            }
            catch (Exception ex)
            {
                // In a real app, log this
                System.Diagnostics.Debug.WriteLine($"Error playing preview: {ex.Message}");
                Stop();
            }
        }

        public void Stop()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }

            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
