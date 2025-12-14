using System.Windows;
using System.Windows.Input;
using Veriflow.Desktop.Models;

namespace Veriflow.Desktop.Views
{
    public partial class ReportNoteWindow : Window
    {
        public bool Saved { get; private set; } = false;

        public ReportNoteWindow(ReportItem item)
        {
            InitializeComponent();
            DataContext = item;

            // Enable Dragging
            MouseLeftButtonDown += (s, e) => DragMove();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Saved = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Saved = false;
            DialogResult = false;
            Close();
        }
    }
}
