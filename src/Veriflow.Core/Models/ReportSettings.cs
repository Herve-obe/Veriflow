using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Veriflow.Core.Models
{
    public class ReportSettings : INotifyPropertyChanged
    {
        private string _customLogoPath = "";
        public string CustomLogoPath
        {
            get => _customLogoPath;
            set { if (_customLogoPath != value) { _customLogoPath = value; OnPropertyChanged(); } }
        }

        private bool _useCustomLogo = false;
        public bool UseCustomLogo
        {
            get => _useCustomLogo;
            set { if (_useCustomLogo != value) { _useCustomLogo = value; OnPropertyChanged(); } }
        }
        
        private string _customTitle = "";
        public string CustomTitle
        {
            get => _customTitle;
            set { if (_customTitle != value) { _customTitle = value; OnPropertyChanged(); } }
        }

        private bool _useCustomTitle = false;
        public bool UseCustomTitle
        {
            get => _useCustomTitle;
            set { if (_useCustomTitle != value) { _useCustomTitle = value; OnPropertyChanged(); } }
        }

        // Visibility Settings - Common
        private bool _showFilename = true;
        public bool ShowFilename { get => _showFilename; set { if (_showFilename != value) { _showFilename = value; OnPropertyChanged(); } } }

        private bool _showScene = true;
        public bool ShowScene { get => _showScene; set { if (_showScene != value) { _showScene = value; OnPropertyChanged(); } } }

        private bool _showTake = true;
        public bool ShowTake { get => _showTake; set { if (_showTake != value) { _showTake = value; OnPropertyChanged(); } } }

        private bool _showTimecode = true;
        public bool ShowTimecode { get => _showTimecode; set { if (_showTimecode != value) { _showTimecode = value; OnPropertyChanged(); } } }

        private bool _showDuration = true;
        public bool ShowDuration { get => _showDuration; set { if (_showDuration != value) { _showDuration = value; OnPropertyChanged(); } } }

        private bool _showNotes = true;
        public bool ShowNotes { get => _showNotes; set { if (_showNotes != value) { _showNotes = value; OnPropertyChanged(); } } }

        // Visibility Settings - Video Specific
        private bool _showFps = true;
        public bool ShowFps { get => _showFps; set { if (_showFps != value) { _showFps = value; OnPropertyChanged(); } } }

        private bool _showIso = true;
        public bool ShowIso { get => _showIso; set { if (_showIso != value) { _showIso = value; OnPropertyChanged(); } } }

        private bool _showWhiteBalance = true;
        public bool ShowWhiteBalance { get => _showWhiteBalance; set { if (_showWhiteBalance != value) { _showWhiteBalance = value; OnPropertyChanged(); } } }

        private bool _showCodecResultion = true;
        public bool ShowCodecResultion { get => _showCodecResultion; set { if (_showCodecResultion != value) { _showCodecResultion = value; OnPropertyChanged(); } } }

        // Visibility Settings - Audio Specific
        private bool _showSampleRate = true;
        public bool ShowSampleRate { get => _showSampleRate; set { if (_showSampleRate != value) { _showSampleRate = value; OnPropertyChanged(); } } }

        private bool _showBitDepth = true;
        public bool ShowBitDepth { get => _showBitDepth; set { if (_showBitDepth != value) { _showBitDepth = value; OnPropertyChanged(); } } }

        private bool _showTracks = true;
        public bool ShowTracks { get => _showTracks; set { if (_showTracks != value) { _showTracks = value; OnPropertyChanged(); } } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ReportSettings Clone()
        {
            return new ReportSettings
            {
                CustomLogoPath = this.CustomLogoPath,
                UseCustomLogo = this.UseCustomLogo,
                CustomTitle = this.CustomTitle,
                UseCustomTitle = this.UseCustomTitle,

                ShowFilename = this.ShowFilename,
                ShowScene = this.ShowScene,
                ShowTake = this.ShowTake,
                ShowTimecode = this.ShowTimecode,
                ShowDuration = this.ShowDuration,
                ShowNotes = this.ShowNotes,

                ShowFps = this.ShowFps,
                ShowIso = this.ShowIso,
                ShowWhiteBalance = this.ShowWhiteBalance,
                ShowCodecResultion = this.ShowCodecResultion,

                ShowSampleRate = this.ShowSampleRate,
                ShowBitDepth = this.ShowBitDepth,
                ShowTracks = this.ShowTracks
            };
        }
    }
}
