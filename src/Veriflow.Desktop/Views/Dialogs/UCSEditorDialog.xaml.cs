using System.Windows;
using Veriflow.Desktop.ViewModels;

namespace Veriflow.Desktop.Views.Dialogs
{
    public partial class UCSEditorDialog : Window
    {
        public UCSEditorDialog()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is UCSEditorViewModel vm)
            {
                vm.OkCommand.Execute(null);
                DialogResult = vm.DialogResult;
            }
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is UCSEditorViewModel vm)
            {
                vm.CancelCommand.Execute(null);
            }
            DialogResult = false;
            Close();
        }
    }
}
