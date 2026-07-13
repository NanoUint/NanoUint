using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NanoUint.Converters;

/// <summary>
/// 将布尔值转换为 Visibility。
/// True → Visible，False → Collapsed。
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        if (value is bool b)
            return (b ^ invert) ? Visibility.Visible : Visibility.Collapsed;
        return invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v == Visibility.Visible;
        return false;
    }
}
