using System.Windows;
using System.Windows.Controls;
using System.Linq;
using GameRes.Formats.Properties;
using GameRes.Formats.Strings;
using System;
using System.Windows.Data;

namespace GameRes.Formats.GUI
{
    /// <summary>
    /// Interaction logic for WidgetWARC.xaml
    /// </summary>
    public partial class WidgetWARC : Grid
    {
        public WidgetWARC ()
        {
            InitializeComponent();
            // select the most recent scheme as default
            if (-1 == Scheme.SelectedIndex)
                Scheme.SelectedIndex = Scheme.ItemsSource.Cast<object>().Count()-1;
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(Scheme.ItemsSource);
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
                if (obj == null)
                    return false;
                var prop = obj.GetType().GetProperty("Name");
                if (prop != null)
                {
                    var name = prop.GetValue(obj) as string;
                    return name != null && name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                // »ØÍË£ºÊ¹ÓÃ ToString()
                var s = obj.ToString();
                return !string.IsNullOrEmpty(s) && s.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            view.Refresh();
        }
    }
}
