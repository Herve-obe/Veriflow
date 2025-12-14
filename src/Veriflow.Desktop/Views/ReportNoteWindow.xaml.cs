using System.Windows;

namespace Veriflow.Desktop.Views
{
    public partial class ReportNoteWindow : Window
    {
        public string NoteText
        {
            get => NoteTextBox.Text;
            set => NoteTextBox.Text = value;
        }

        public ReportNoteWindow(string initialNote)
        {
            InitializeComponent();
            NoteText = initialNote ?? string.Empty;
            
            // Focus text box
            Loaded += (s, e) => NoteTextBox.Focus();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
