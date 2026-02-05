using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace RszViewer
{
    public class LinkIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string type)
            {
                if (type == "Linked") return "🔗";
                if (type == "Potentially Linked") return "❓";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class PathToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path) return Path.GetFileName(path);
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter as string == "hideIfZero")
            {
                if (value is int count && count > 0) return Visibility.Visible;
                return Visibility.Collapsed;
            }

            if (value is bool b) return b ? Visibility.Visible : Visibility.Collapsed;
            if (value is int i) return i > 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return Application.Current.FindResource("AccentOrange");
            return Application.Current.FindResource("BgTertiary");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
