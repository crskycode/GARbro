using System.Windows;

namespace GameRes.Formats.Ethornell
{
    /// <summary>
    /// Interaction logic for KeyFileWarningDialog.xaml
    /// </summary>
    public partial class KeyFileWarningDialog : Window
    {
        public KeyFileWarningDialog()
        {
            InitializeComponent();
        }

        public bool DontShowAgainChecked => DontShowAgain.IsChecked ?? false;

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}