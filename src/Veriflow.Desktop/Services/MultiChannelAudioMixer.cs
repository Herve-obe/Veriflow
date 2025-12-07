using NAudio.Wave;
using System;
using System.Linq;

namespace Veriflow.Desktop.Services
{
    /// <summary>
    /// Custom ISampleProvider that downmixes multi-channel input to Stereo output
    /// and allows individual volume/mute control + Panning.
    /// </summary>
    public class MultiChannelAudioMixer : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly float[] _channelVolumes;
        private readonly bool[] _channelMutes;
        private readonly bool[] _channelSolos;
        private readonly float[] _channelPans;
        private readonly int _inputChannels;
        private float[] _sourceBuffer;

        // Force Stereo Output (IEEE Float)
        public WaveFormat WaveFormat { get; }

        public MultiChannelAudioMixer(ISampleProvider source)
        {
            _source = source;
            _inputChannels = source.WaveFormat.Channels;
            
            // Output is always Stereo, same SampleRate
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_source.WaveFormat.SampleRate, 2);

            _channelVolumes = new float[_inputChannels];
            _channelMutes = new bool[_inputChannels];
            _channelSolos = new bool[_inputChannels];
            _channelPans = new float[_inputChannels];
            
            // Buffer to hold raw input samples before downmix
            // Initial size, will grow if needed
            _sourceBuffer = new float[_inputChannels * 1024]; 

            // Default: All channels active, full volume, Center pan
            for (int i = 0; i < _inputChannels; i++)
            {
                _channelVolumes[i] = 1.0f;
                _channelMutes[i] = false;
                _channelSolos[i] = false;
                _channelPans[i] = 0.0f; // Center
            }
        }

        public void SetChannelPan(int channel, float pan)
        {
            if (channel >= 0 && channel < _inputChannels)
            {
                // Clamp between -1 and 1
                if (pan < -1.0f) pan = -1.0f;
                if (pan > 1.0f) pan = 1.0f;
                _channelPans[channel] = pan;
            }
        }

        public void SetChannelVolume(int channel, float volume)
        {
            if (channel >= 0 && channel < _inputChannels)
            {
                _channelVolumes[channel] = volume;
            }
        }

        public void SetChannelMute(int channel, bool muted)
        {
            if (channel >= 0 && channel < _inputChannels)
            {
                _channelMutes[channel] = muted;
            }
        }

        public void SetChannelSolo(int channel, bool soloed)
        {
             if (channel >= 0 && channel < _inputChannels)
            {
                _channelSolos[channel] = soloed;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // 'count' is the number of samples requested for the OUTPUT (Stereo).
            // So number of output frames = count / 2.
            int outputFrames = count / 2;
            int samplesToReadFromSource = outputFrames * _inputChannels;

            // Ensure source buffer is big enough
            if (_sourceBuffer.Length < samplesToReadFromSource)
            {
                _sourceBuffer = new float[samplesToReadFromSource];
            }

            // Read raw multi-channel data
            int sourceSamplesRead = _source.Read(_sourceBuffer, 0, samplesToReadFromSource);
            int framesRead = sourceSamplesRead / _inputChannels;

            // Check if ANY track is SOLOED
            bool anySolo = false;
            for (int i = 0; i < _inputChannels; i++)
            {
                if (_channelSolos[i])
                {
                    anySolo = true;
                    break;
                }
            }

            // Process each frame
            int outIndex = offset;
            
            for (int frame = 0; frame < framesRead; frame++)
            {
                float sumLeft = 0;
                float sumRight = 0;
                int inputOffset = frame * _inputChannels;

                for (int ch = 0; ch < _inputChannels; ch++)
                {
                    // Logic:
                    // If AnySolo is TRUE -> Only play IsSoloed tracks.
                    // If AnySolo is FALSE -> Play All EXCEPT IsMuted tracks.

                    bool isAudible = false;
                    if (anySolo)
                    {
                        if (_channelSolos[ch]) isAudible = true;
                    }
                    else
                    {
                        if (!_channelMutes[ch]) isAudible = true;
                    }

                    if (isAudible)
                    {
                        float sample = _sourceBuffer[inputOffset + ch];
                        float vol = _channelVolumes[ch];
                        float pan = _channelPans[ch]; // -1.0 to 1.0

                        // Pan Calculation (Linear Panning)
                        // Center (0) -> Left: 0.5, Right: 0.5
                        // Left (-1) -> Left: 1.0, Right: 0.0
                        // Right (1) -> Left: 0.0, Right: 1.0
                        
                        float gainLeft = (1.0f - pan) / 2.0f;
                        float gainRight = (1.0f + pan) / 2.0f;

                        // Apply
                        float processed = sample * vol;
                        
                        sumLeft += processed * gainLeft;
                        sumRight += processed * gainRight;
                    }
                }

                buffer[outIndex++] = sumLeft; 
                buffer[outIndex++] = sumRight;
            }

            return framesRead * 2;
        }
    }
}
