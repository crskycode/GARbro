using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using GameRes.Formats.KiriKiri;
using GameRes.Formats.Strings;

namespace GameRes.Formats.GUI
{
    /// <summary>
    /// Interaction logic for WidgetXP3.xaml
    /// </summary>
    public partial class WidgetXP3 : StackPanel
    {
        public WidgetXP3()
        {
            var last_selected = Properties.Settings.Default.XP3Scheme;
            InitializeComponent();
            var keys = new[] { new KeyValuePair<string, ICrypt>(arcStrings.ArcNoEncryption, Xp3Opener.NoCryptAlgorithm) };
            this.DataContext = keys.Concat(Xp3Opener.KnownSchemes.OrderBy(x => x.Key));
            this.Loaded += (s, e) => {
                if (!string.IsNullOrEmpty(last_selected))
                    this.Scheme.SelectedValue = last_selected;
                else
                    this.Scheme.SelectedIndex = 0;

                // 确保初始时没有筛选
                var view = CollectionViewSource.GetDefaultView(this.DataContext);
                if (view != null)
                    view.Filter = null;
            };
        }

        public ICrypt GetScheme()
        {
            return Xp3Opener.GetScheme(Scheme.SelectedValue as string);
        }

        // TextBox 的 TextChanged 事件处理：按 Key（方案名）做不区分大小写的子串匹配
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(this.DataContext);
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
                if (obj is KeyValuePair<string, ICrypt> kvp)
                    return kvp.Key.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                return false;
            };
            view.Refresh();
        }
    }

    internal class ClassNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value != null)
                return value.GetType().Name;
            else
                return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}