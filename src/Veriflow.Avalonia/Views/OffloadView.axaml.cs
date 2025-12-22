using Avalonia.Controls;
using Veriflow.Avalonia.ViewModels;
using Veriflow.Avalonia.Services;

namespace Veriflow.Avalonia.Views;

public partial class OffloadView : UserControl
{
    public OffloadView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is OffloadViewModel viewModel)
        {
            viewModel.RequestFolderPicker += OnRequestFolderPicker;
        }
    }

    private async void OnRequestFolderPicker(string target)
    {
        if (DataContext is not OffloadViewModel viewModel) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var folder = await FilePickerService.PickFolderAsync(topLevel.StorageProvider);
        if (!string.IsNullOrEmpty(folder))
        {
            switch (target)
            {
                case "source":
                    viewModel.SourcePath = folder;
                    break;
                case "dest1":
                    viewModel.Destination1Path = folder;
                    break;
                case "dest2":
                    viewModel.Destination2Path = folder;
                    break;
            }
        }
    }
}
