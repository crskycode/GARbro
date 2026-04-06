using GameRes.Formats.Cyberworks;
using GameRes.Formats.Strings;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System;

namespace GameRes.Formats.GUI
{
    /// <summary>
    /// Interaction logic for WidgetBELL.xaml
    /// </summary>
    public partial class WidgetBELL : StackPanel
    {
        public WidgetBELL()
        {
            InitializeComponent();
            var keys = new string[] { arcStrings.ArcIgnoreEncryption };
            Title.ItemsSource = keys.Concat (DatOpener.KnownSchemes.Keys.OrderBy (x => x));
            if (-1 == Title.SelectedIndex)
                Title.SelectedIndex = 0;
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(Title.ItemsSource);
            if (view == null)
                return;

            var text = SearchBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                view.Filter = null;
                view.Refresh();
                return;
            }

            view.Filter = obj =>
            {
                var s = obj as string;
                return s != null && s.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            view.Refresh();
        }
    }
}
