using System.Windows;

namespace Veriflow.Desktop.Views
{
    public partial class ReportTemplatesWindow : Window
    {
        public ReportTemplatesWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
