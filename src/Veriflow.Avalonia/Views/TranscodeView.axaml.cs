using Avalonia.Controls;
using Veriflow.Avalonia.ViewModels;
using Veriflow.Avalonia.Services;
using System.Linq;

namespace Veriflow.Avalonia.Views;

public partial class TranscodeView : UserControl
{
    public TranscodeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // TranscodeViewModel doesn't have RequestFilePicker yet
        // Will be added when needed
    }

    private async void OnRequestFilePicker()
    {
        if (DataContext is not TranscodeViewModel viewModel) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var files = await FilePickerService.PickMultipleFilesAsync(topLevel.StorageProvider);
        if (files.Any())
        {
            viewModel.AddFiles(files);
        }
    }
}
