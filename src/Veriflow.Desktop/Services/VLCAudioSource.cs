using CSCore;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Veriflow.Desktop.Services
{
    /// <summary>
    /// A high-performance lock-based circular buffer implementation for audio streaming.
    /// Replaces the inefficient ConcurrentQueue<float>.
    /// </summary>
    public class VLCAudioSource : ISampleSource
    {
        private readonly float[] _buffer;
        private readonly int _bufferSize;
        private int _writeIndex;
        private int _readIndex;
        private int _sampleCount; // Number of samples valid in buffer
        private readonly object _lock = new();

        // 1 second buffer for 16 channels @ 48kHz = 768,000 floats (~3MB).
        // Let's use 2 seconds to be safe against jitter.
        private const int BufferDurationSeconds = 2;

        private readonly int _channels;
        private readonly int _sampleRate;

        // Temporary buffer for marshaling to avoid frequent small allocs? 
        // Actually we can marshal directly to ring buffer if we handle wrap-around, 
        // but Marshal.Copy expects contiguous array. 
        // We will keep a small scratch buffer for incoming data to avoid allocs every frame.
        private float[] _marshalBuffer = new float[4096 * 16]; // Max expected frame size
        
        public WaveFormat WaveFormat { get; }

        public bool CanSeek => false;
        public long Position { get => 0; set { } }
        public long Length => 0;

        public VLCAudioSource(int sampleRate = 48000, int channels = 2)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            WaveFormat = new WaveFormat(sampleRate, 32, channels, AudioEncoding.IeeeFloat);
            
            _bufferSize = sampleRate * channels * BufferDurationSeconds;
            _buffer = new float[_bufferSize];
        }

        // Called by VLC Callback (Push)
        public void Write(IntPtr samples, uint count)
        {
            int samplesToWrite = (int)(count * _channels); 
            
            // Resize scratch buffer if needed (unlikely)
            if (samplesToWrite > _marshalBuffer.Length)
            {
                Array.Resize(ref _marshalBuffer, samplesToWrite);
            }

            // Copy from Unmanaged to Managed Scratch Buffer
            Marshal.Copy(samples, _marshalBuffer, 0, samplesToWrite);

            lock (_lock)
            {
                // If buffer full, we must drop data or overwrite. 
                // Overwriting oldest is better for live stream to catch up? 
                // Or dropping newest? 
                // For a player, if we are full, it means consumer is too slow. 
                // We should probably just overwrite safely or block? Blocking callbacks hangs VLC.
                // Let's overwrite (circular).
                
                // Wait, typically we just write.
                
                int freeSpace = _bufferSize - _sampleCount;
                if (samplesToWrite > freeSpace)
                {
                    // Buffer overrun. Reset to prevent glitch train? 
                    // Or just advance read head?
                    // Let's simple-mindedly drop new data? No, choppy.
                    // Advance Read Head (Simulation of skipping old audio).
                    
                    int overflow = samplesToWrite - freeSpace;
                    _readIndex = (_readIndex + overflow) % _bufferSize;
                    _sampleCount -= overflow; 
                    // Now freeSpace == samplesToWrite
                }

                // Write to Ring Buffer
                int firstChunk = Math.Min(samplesToWrite, _bufferSize - _writeIndex);
                Array.Copy(_marshalBuffer, 0, _buffer, _writeIndex, firstChunk);
                
                if (firstChunk < samplesToWrite)
                {
                    int secondChunk = samplesToWrite - firstChunk;
                    Array.Copy(_marshalBuffer, firstChunk, _buffer, 0, secondChunk);
                }

                _writeIndex = (_writeIndex + samplesToWrite) % _bufferSize;
                _sampleCount += samplesToWrite;
            }
        }

        public void Write(float[] samples, int count)
        {
           // Similar logic for float[] input if needed
        }

        // Called by Consumer (Pull)
        public int Read(float[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                int samplesAvailable = _sampleCount;
                int samplesToRead = Math.Min(count, samplesAvailable);

                if (samplesToRead == 0)
                {
                    // Buffer Underrun. Output Silence.
                    Array.Clear(buffer, offset, count);
                    return count; 
                }

                int firstChunk = Math.Min(samplesToRead, _bufferSize - _readIndex);
                Array.Copy(_buffer, _readIndex, buffer, offset, firstChunk);

                if (firstChunk < samplesToRead)
                {
                    int secondChunk = samplesToRead - firstChunk;
                    Array.Copy(_buffer, 0, buffer, offset + firstChunk, secondChunk);
                }

                _readIndex = (_readIndex + samplesToRead) % _bufferSize;
                _sampleCount -= samplesToRead;

                // Pad remaining if any
                if (samplesToRead < count)
                {
                    Array.Clear(buffer, offset + samplesToRead, count - samplesToRead);
                    return count;
                }

                return samplesToRead;
            }
        }

        public void Dispose()
        {
            _sampleCount = 0;
            _readIndex = 0;
            _writeIndex = 0;
        }
    }
}
