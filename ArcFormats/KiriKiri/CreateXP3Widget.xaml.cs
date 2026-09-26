using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using GameRes.Formats.KiriKiri;
using GameRes.Formats.Strings;
using Microsoft.Win32;

namespace GameRes.Formats.GUI
{
    /// <summary>
    /// Interaction logic for CreateXP3Widget.xaml
    /// </summary>
    public partial class CreateXP3Widget : Grid
    {
        public CreateXP3Widget ()
        {
            InitializeComponent ();
            EncryptionWidget.Scheme.SelectionChanged += Scheme_SelectionChanged;
            UpdateTemplateRow ();
            this.Loaded += (s, e) => UpdateTemplateRow ();
        }

        void Scheme_SelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            ICrypt scheme = null;
            if (0 != e.AddedItems.Count)
                scheme = ((KeyValuePair<string, ICrypt>)e.AddedItems[0]).Value;
            TemplateRow.Visibility = scheme is HxCrypt ? Visibility.Visible : Visibility.Collapsed;
        }

        void UpdateTemplateRow ()
        {
            TemplateRow.Visibility = EncryptionWidget.GetScheme () is HxCrypt ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Browse_Click (object sender, RoutedEventArgs e)
        {
            string initial = IndexTemplate.Text;
            string dir = ".";
            if (!string.IsNullOrEmpty (initial))
            {
                var parent = Directory.GetParent (initial);
                if (null != parent)
                {
                    dir = parent.FullName;
                    initial = Path.GetFileName (initial);
                }
            }
            dir = Path.GetFullPath (dir);
            var dlg = new OpenFileDialog {
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = "XP3 archives (*.xp3)|*.xp3|All files (*.*)|*.*",
                FileName = initial,
                InitialDirectory = dir,
                Multiselect = false,
                Title = arcStrings.XP3LabelIndexTemplate,
            };
            var owner = FindVisualParent<Window> (this);
            if (dlg.ShowDialog (owner).Value && !string.IsNullOrEmpty (dlg.FileName))
                IndexTemplate.Text = dlg.FileName;
        }

        static parentItem FindVisualParent<parentItem> (DependencyObject obj) where parentItem : DependencyObject
        {
            if (null == obj)
                return null;
            DependencyObject parent = VisualTreeHelper.GetParent (obj);
            while (parent != null && !(parent is parentItem))
            {
                parent = VisualTreeHelper.GetParent (parent);
            }
            return parent as parentItem;
        }
    }

    public class Xp3VersionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (int)value;
            switch (val)
            {
                case 1: return "1";
                case 2: return "2";
                case 3: return "Z";
                default: throw new NotImplementedException();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value.ToString();
            switch (str)
            {
                case "1": return 1;
                case "2": return 2;
                case "Z": return 3;
                default: throw new NotImplementedException();
            }
        }
    }
}
