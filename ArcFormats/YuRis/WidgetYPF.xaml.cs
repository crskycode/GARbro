using GameRes.Formats.Properties;
using GameRes.Formats.Strings;
using GameRes.Formats.YuRis;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System;

namespace GameRes.Formats.GUI
{
    /// <summary>
    /// Interaction logic for WidgetYPF.xaml
    /// </summary>
    public partial class WidgetYPF : StackPanel
    {
        public WidgetYPF ()
        {
            InitializeComponent();
            var guess = new Dictionary<string, YpfScheme> { { arcStrings.YPFTryGuess, null } };
            Scheme.ItemsSource = guess.Concat (YpfOpener.KnownSchemes.OrderBy (x => x.Key));
            if (-1 == Scheme.SelectedIndex)
                Scheme.SelectedIndex = 0;
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
                if (obj is KeyValuePair<string, YpfScheme> kvp)
                    return kvp.Key.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                if (obj is string s)
                    return s.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                return false;
            };
            view.Refresh();
        }
    }
}
