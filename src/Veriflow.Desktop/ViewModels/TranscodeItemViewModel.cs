using CommunityToolkit.Mvvm.ComponentModel;

namespace Veriflow.Desktop.ViewModels
{
    public partial class TranscodeItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _filePath;

        [ObservableProperty]
        private string _fileName;
        
        [ObservableProperty]
        private string _status = "Pending"; // Pending, Processing, Done, Error

        [ObservableProperty]
        private double _progressValue = 0;

        [ObservableProperty]
        private string _durationString = "";

        [ObservableProperty]
        private string _audioInfo = ""; // e.g. "48kHz | 24-bit"

        public TranscodeItemViewModel(string path)
        {
            FilePath = path;
            FileName = System.IO.Path.GetFileName(path);
        }
    }
}
