using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace GameRes.Formats.GUI
{
    public partial class CreateBGIWidget : Grid
    {
        /// <summary>
        /// Interaction logic for CreateBGIWidget.xaml
        /// </summary>
        public CreateBGIWidget()
        {
            InitializeComponent();
        }

        private void BrowseKeyFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select BGI Key File",
                Filter = "Key files (*.dat)|*.dat|All files (*.*)|*.*",
                FileName = "bgi_keys.dat"
            };

            if (dialog.ShowDialog() == true)
            {
                KeyFilePath.Text = dialog.FileName;
            }
        }
    }
}