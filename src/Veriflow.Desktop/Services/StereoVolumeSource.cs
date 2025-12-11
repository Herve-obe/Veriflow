using CSCore;
using System;

namespace Veriflow.Desktop.Services
{
    public class StereoVolumeSource : SampleAggregatorBase
    {
        public float LeftVolume { get; set; } = 1.0f;
        public float RightVolume { get; set; } = 1.0f;
        public bool IsLeftMuted { get; set; }
        public bool IsRightMuted { get; set; }

        public StereoVolumeSource(ISampleSource source) : base(source)
        {
        }

        public override int Read(float[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, count);
            int channels = WaveFormat.Channels;

            for (int i = 0; i < read; i++)
            {
                int bufferIndex = offset + i;
                int channelIndex = i % channels;

                // Simple Stereo Mapping: Left = Even indices (0, 2...), Right = Odd indices (1, 3...)
                bool isLeft = (channelIndex % 2) == 0;

                if (isLeft)
                {
                    if (IsLeftMuted)
                        buffer[bufferIndex] = 0;
                    else
                        buffer[bufferIndex] *= LeftVolume;
                }
                else
                {
                    if (IsRightMuted)
                        buffer[bufferIndex] = 0;
                    else
                        buffer[bufferIndex] *= RightVolume;
                }
            }

            return read;
        }
    }
}
