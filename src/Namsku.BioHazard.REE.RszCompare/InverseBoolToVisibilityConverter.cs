using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

using System.Windows.Markup;

namespace ReeCompare
{
    public class InverseBoolToVisibilityConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            if (value is int i)
            {
                if (parameter?.ToString() == "hideIfZero")
                {
                    return i > 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                return i > 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            if (value is Visibility v)
            {
                return v == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
