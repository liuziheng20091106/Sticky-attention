using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StickyHomeworks.Converter
{
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }

            // 返回一个默认的 Brush（可以是透明色），以防 value 不是 Color 类型或为空
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return null;
        }
    }
}
