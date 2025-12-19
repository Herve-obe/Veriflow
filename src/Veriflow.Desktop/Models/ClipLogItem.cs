using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Veriflow.Desktop.Models
{
    public partial class ClipLogItem : ObservableObject
    {
        [ObservableProperty]
        private string _inPoint = "";

        [ObservableProperty]
        private string _outPoint = "";

        [ObservableProperty]
        private string _duration = "";

        [ObservableProperty]
        private string _notes = "";

        [ObservableProperty]
        private string _tagColor = "#555555"; // Default grey

        [ObservableProperty]
        private bool _isEditing = false;
    }
}
