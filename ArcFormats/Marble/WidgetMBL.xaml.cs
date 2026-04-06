using GameRes.Formats.Marble;
using GameRes.Formats.Strings;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System;
using System.Windows.Data;

namespace GameRes.Formats.GUI
{
    /// <summary>
    /// Interaction logic for WidgetMBL.xaml
    /// </summary>
    public partial class WidgetMBL : Grid
    {
        public WidgetMBL ()
        {
            InitializeComponent ();
            var keys = new[] { new KeyValuePair<string, string> (arcStrings.ArcDefault, "") };
            EncScheme.ItemsSource = keys.Concat (MblOpener.KnownKeys);
            if (-1 == EncScheme.SelectedIndex)
                EncScheme.SelectedIndex = 0;
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(EncScheme.ItemsSource);
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
                if (obj is KeyValuePair<string, string> kvp)
                    return kvp.Key.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                if (obj is string s)
                    return s.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                return false;
            };
            view.Refresh();
        }
    }
}
