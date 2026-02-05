using System;
using System.Globalization;
using System.Windows.Data;

namespace RszViewer
{
    public class ShortNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                // If text contains spaces (like "via.Transform : via.Transform"), split by space first
                if (text.Contains(" : "))
                {
                    var parts = text.Split(new[] { " : " }, StringSplitOptions.None);
                    var name = GetShortName(parts[0]);
                    var type = GetShortName(parts[1]);
                    return $"{name} : {type}";
                }

                return GetShortName(text);
            }
            return value;
        }

        private string GetShortName(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // If it's a file path, get filename
            if (text.Contains("\\") || text.Contains("/"))
            {
                return System.IO.Path.GetFileName(text);
            }

            // If it's a dot-separated path, get last part
            int lastDot = text.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < text.Length - 1)
            {
                return text.Substring(lastDot + 1);
            }

            return text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
