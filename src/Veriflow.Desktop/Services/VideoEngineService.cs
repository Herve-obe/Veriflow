using LibVLCSharp.Shared;
using System;

namespace Veriflow.Desktop.Services
{
    public class VideoEngineService
    {
        private static VideoEngineService? _instance;
        public static VideoEngineService Instance => _instance ??= new VideoEngineService();

        public LibVLC? LibVLC { get; private set; }

        public void Initialize()
        {
            if (LibVLC != null) return;

            LibVLCSharp.Shared.Core.Initialize();

            var options = new string[]
            {
                "--avcodec-hw=d3d11va",     // Keep GPU Acceleration
                "--aout=mmdevice",          // Force WASAPI (Low Latency Audio)
                "--file-caching=1000",      // Balanced buffer for specific Direct Audio stability
                "--network-caching=1000",
                "--clock-jitter=0",
                "--clock-synchro=0"
            };
            LibVLC = new LibVLC(options);
        }
    }
}
