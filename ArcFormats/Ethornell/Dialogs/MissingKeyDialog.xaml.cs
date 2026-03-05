using System.Windows;

namespace GameRes.Formats.Ethornell
{
    public enum MissingKeyAction
    {
        PackUncompressed,
        Skip,
        Cancel
    }

    /// <summary>
    /// Interaction logic for MissingKeyDialog.xaml
    /// </summary>
    public partial class MissingKeyDialog : Window
    {
        public MissingKeyAction Action { get; private set; }
        public bool ApplyToAllChecked => ApplyToAll.IsChecked ?? false;

        public MissingKeyDialog(string fileName)
        {
            InitializeComponent();
            FileNameRun.Text = fileName;
        }

        private void PackUncompressed_Click(object sender, RoutedEventArgs e)
        {
            Action = MissingKeyAction.PackUncompressed;
            DialogResult = true;
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            Action = MissingKeyAction.Skip;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Action = MissingKeyAction.Cancel;
            DialogResult = false;
        }
    }
}