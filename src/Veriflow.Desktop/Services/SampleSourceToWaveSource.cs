using CSCore;
using System;

namespace Veriflow.Desktop.Services
{
    public class SampleSourceToWaveSource : IWaveSource
    {
        private readonly ISampleSource _source;

        public SampleSourceToWaveSource(ISampleSource source)
        {
            if (source.WaveFormat.WaveFormatTag != AudioEncoding.IeeeFloat)
                throw new ArgumentException("Source must be IEEE Float", nameof(source));
                
            _source = source;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public bool CanSeek => _source.CanSeek;

        public long Position
        {
            get => _source.Position; // Position is in samples? No, generic Position property.
            set => _source.Position = value;
        }

        public long Length => _source.Length; // Length in samples? No.

        public int Read(byte[] buffer, int offset, int count)
        {
            // Count IS IN BYTES.
            // We need to read FLOATS.
            // 4 bytes per float.
            int floatsToRead = count / 4;
            float[] tempBuffer = new float[floatsToRead];
            
            int samplesRead = _source.Read(tempBuffer, 0, floatsToRead);
            
            if (samplesRead > 0)
            {
                Buffer.BlockCopy(tempBuffer, 0, buffer, offset, samplesRead * 4);
            }
            
            return samplesRead * 4;
        }

        public void Dispose()
        {
            // _source may be disposed here or externally. 
            // Usually valid to verify ownership.
        }
    }
}
