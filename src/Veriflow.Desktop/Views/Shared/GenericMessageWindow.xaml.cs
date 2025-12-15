using System.Windows;
using Veriflow.Desktop.ViewModels;

namespace Veriflow.Desktop.Views.Shared
{
    public partial class GenericMessageWindow : Window
    {
        public GenericMessageWindow(string message, string title = "Veriflow")
        {
            InitializeComponent();
            Title = title;
            MessageText.Text = message;
            
            // Apply Owner if possible for centering
            if (Application.Current != null && Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                Owner = Application.Current.MainWindow;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
