using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Veriflow.Desktop.Models;

namespace Veriflow.Desktop.Services
{
    public class BwfMetadataReader
    {
        // Removed broken legacy methods. Use ReadMetadataFromStream.
        
        // Revised approach: Open Stream manually to parse chunks.
        // NAudio's WaveFileReader populates ExtraChunks with positions.
        
        public AudioMetadata ReadMetadataFromStream(string filePath)
        {
            var metadata = new AudioMetadata
            {
                Filename = System.IO.Path.GetFileName(filePath)
            };

            try
            {
                using var reader = new WaveFileReader(filePath);
                metadata.Format = $"{reader.WaveFormat.SampleRate}Hz / {reader.WaveFormat.BitsPerSample}bit";
                metadata.Duration = reader.TotalTime.ToString(@"hh\:mm\:ss");
                metadata.ChannelCount = reader.WaveFormat.Channels;

                foreach (var chunk in reader.ExtraChunks)
                {
                    if (chunk.IdentifierAsString.ToLower() == "bext")
                    {
                        var data = GetChunkData(filePath, chunk.StreamPosition, chunk.Length);
                        ParseBextBytes(data, metadata, reader.WaveFormat.SampleRate);
                    }
                    else if (chunk.IdentifierAsString.ToLower() == "ixml")
                    {
                        var data = GetChunkData(filePath, chunk.StreamPosition, chunk.Length);
                        ParseIXmlBytes(data, metadata);
                    }
                }
            }
            catch { }
            return metadata;
        }

        private byte[] GetChunkData(string path, long position, int length)
        {
            using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            fs.Position = position;
            byte[] buffer = new byte[length];
            fs.Read(buffer, 0, length);
            return buffer;
        }

        private void ParseBextBytes(byte[] data, AudioMetadata metadata, int sampleRate)
        {
            // BEXT Structure (EBU Tech 3285)
            // Description: 256 bytes (ASCII)
            // Originator: 32 bytes
            // OriginatorRef: 32 bytes
            // OriginationDate: 10 bytes (YYYY-MM-DD)
            // OriginationTime: 8 bytes (HH:MM:SS)
            // TimeReferenceLow: 4 bytes (uint) -> Samples since midnight
            // TimeReferenceHigh: 4 bytes (uint)
            // Version: 2 bytes
            // UMID: 64 bytes
            // ...
            
            if (data.Length < 256 + 32 + 32 + 10 + 8 + 8) return;

            metadata.Scene = GetString(data, 0, 256).Trim(); // Often Description holds Scene/Take in notes, but iXML is better. 
            // NOTE: Description is generic.
            
            // Standard approach: Description often used for notes.
            // Originator often holds Recorder Name.
            metadata.Originator = GetString(data, 256, 32).Trim();
            metadata.CreationDate = GetString(data, 256 + 32 + 32, 10) + " " + GetString(data, 256 + 32 + 32 + 10, 8);

            // Timecode
            long low = BitConverter.ToUInt32(data, 256 + 32 + 32 + 10 + 8);
            long high = BitConverter.ToUInt32(data, 256 + 32 + 32 + 10 + 8 + 4);
            long samplesSinceMidnight = low + (high << 32);

            metadata.TimecodeStart = SamplesToTimecode(samplesSinceMidnight, sampleRate);
        }

        private void ParseIXmlBytes(byte[] data, AudioMetadata metadata)
        {
            try
            {
                string xmlString = Encoding.UTF8.GetString(data).Trim('\0');
                var doc = new XmlDocument();
                doc.LoadXml(xmlString);

                // Project/Tape extraction
                var projectNode = doc.SelectSingleNode("//PROJECT");
                var tapeNode = doc.SelectSingleNode("//TAPE");
                var sceneNode = doc.SelectSingleNode("//SCENE");
                var takeNode = doc.SelectSingleNode("//TAKE");
                
                if (sceneNode != null) metadata.Scene = sceneNode.InnerText;
                if (takeNode != null) metadata.Take = takeNode.InnerText;
                if (tapeNode != null) metadata.Tape = tapeNode.InnerText;

                // Track Names
                var trackNodes = doc.SelectNodes("//TRACK_LIST/TRACK");
                if (trackNodes != null)
                {
                    metadata.TrackNames.Clear();
                    // Sort by channel index just in case
                    var tracks = new SortedDictionary<int, string>();
                    
                    foreach (XmlNode node in trackNodes)
                    {
                         var indexNode = node.SelectSingleNode("CHANNEL_INDEX");
                         var nameNode = node.SelectSingleNode("NAME");
                         
                         if (indexNode != null && nameNode != null && int.TryParse(indexNode.InnerText, out int idx))
                         {
                             // iXML is 1-based usually
                             tracks[idx] = nameNode.InnerText;
                         }
                    }

                    // Metadata might have gaps, but let's just populate list
                    foreach(var kvp in tracks)
                    {
                        metadata.TrackNames.Add(kvp.Value);
                    }
                }
            }
            catch { }
        }

        private string GetString(byte[] buffer, int offset, int length)
        {
            return Encoding.ASCII.GetString(buffer, offset, length).Trim('\0');
        }

        private string SamplesToTimecode(long samples, int sampleRate)
        {
            if (sampleRate == 0) return "00:00:00:00";
            
            // Assume 24fps, 25fps, 30fps? Or use Timecode rate if available.
            // BEXT doesn't strictly define FPS.
            // Standard approach: Audio Time (H:M:S) is easy. Frames is variable.
            // Lets just do: HH:MM:SS
            
            double totalSeconds = (double)samples / sampleRate;
            TimeSpan t = TimeSpan.FromSeconds(totalSeconds);
            return t.ToString(@"hh\:mm\:ss");
        }
    }
}
