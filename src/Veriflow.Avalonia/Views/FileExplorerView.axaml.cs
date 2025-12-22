using Avalonia.Controls;
using Veriflow.Avalonia.ViewModels;

namespace Veriflow.Avalonia.Views;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
        DataContext = new FileExplorerViewModel();
    }
}
