using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NanoUint.Converters;

/// <summary>
/// 将 System.Windows.Media.Color 转换为 SolidColorBrush，用于 XAML 绑定。
/// </summary>
public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Color color)
            return new SolidColorBrush(color);
        return new SolidColorBrush(Color.FromRgb(10, 10, 10));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
            return brush.Color;
        return Color.FromRgb(10, 10, 10);
    }
}
