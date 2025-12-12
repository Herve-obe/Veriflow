using CSCore;
using CSCore.Codecs;
using CSCore.SoundOut;
using CSCore.Streams;
using System;
using System.IO;

namespace Veriflow.Desktop.Services
{
    public class AudioPreviewService : IDisposable
    {
        private ISoundOut? _outputDevice;
        private IWaveSource? _audioSource;

        public void Play(string filePath)
        {
            Stop(); 

            try
            {
                if (!File.Exists(filePath)) return;

                // Try Native Codecs first (Faster, Seekable)
                try
                {
                    _audioSource = CodecFactory.Instance.GetCodec(filePath);
                }
                catch (Exception)
                {
                    // Fallback to FFmpeg Pipe (Slower start, Non-seekable, universal support)
                    System.Diagnostics.Debug.WriteLine("[AudioPreview] Native codec failed, trying FFmpeg fallback...");
                    try 
                    {
                         _audioSource = new FFmpegWaveSource(filePath);
                    }
                    catch (Exception exFF)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AudioPreview] FFmpeg fallback failed: {exFF.Message}");
                        return; // Give up
                    }
                }

                if (_audioSource == null) return;

                if (_audioSource.WaveFormat.Channels > 1)
                {
                    var sampleSource = _audioSource.ToSampleSource();
                    var monoSource = new MonoSampleDownmixer(sampleSource);
                    _audioSource = monoSource.ToWaveSource();
                }

                _outputDevice = new WasapiOut();
                _outputDevice.Initialize(_audioSource);
                _outputDevice.Play();
            }
            catch (Exception ex)
            {
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

            if (_audioSource != null)
            {
                _audioSource.Dispose();
                _audioSource = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private class MonoSampleDownmixer : ISampleSource
        {
            private readonly ISampleSource _source;
            public WaveFormat WaveFormat { get; }

            public bool CanSeek => _source.CanSeek;
            public long Position
            {
                get => _source.Position / _source.WaveFormat.Channels;
                set => _source.Position = value * _source.WaveFormat.Channels;
            }
            public long Length => _source.Length / _source.WaveFormat.Channels;

            public MonoSampleDownmixer(ISampleSource source)
            {
                _source = source;
                if (source.WaveFormat.Channels == 1)
                    throw new ArgumentException("Source is already Mono");

                WaveFormat = new WaveFormat(source.WaveFormat.SampleRate, 32, 1, AudioEncoding.IeeeFloat);
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int inputChannels = _source.WaveFormat.Channels;
                int sourceSamplesToRead = count * inputChannels;
                float[] sourceBuffer = new float[sourceSamplesToRead];

                int read = _source.Read(sourceBuffer, 0, sourceSamplesToRead);

                int outputSamples = read / inputChannels;

                for (int i = 0; i < outputSamples; i++)
                {
                    float sum = 0;
                    for (int ch = 0; ch < inputChannels; ch++)
                    {
                        sum += sourceBuffer[i * inputChannels + ch];
                    }
                    buffer[offset + i] = sum / inputChannels;
                }

                return outputSamples;
            }

            public void Dispose()
            {
                (_source as IDisposable)?.Dispose();
            }
        }
    }
}
