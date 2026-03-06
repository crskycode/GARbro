using System.Windows;

namespace GameRes.Formats.Ethornell
{
    /// <summary>
    /// Interaction logic for MissingKeyFileDialog.xaml
    /// </summary>
    public partial class MissingKeyFileDialog : Window
    {
        public bool PackUncompressed { get; private set; }

        public MissingKeyFileDialog()
        {
            InitializeComponent();
        }

        private void PackUncompressed_Click(object sender, RoutedEventArgs e)
        {
            PackUncompressed = true;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            PackUncompressed = false;
            DialogResult = false;
        }
    }
}