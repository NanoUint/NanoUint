using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using NanoUint.Models;

namespace NanoUint.Converters;

/// <summary>将 MessageDirection 或 IsOutgoing 转换为聊天气泡的 HorizontalAlignment</summary>
public class MessageAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MessageDirection direction)
            return direction == MessageDirection.Outgoing ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        if (value is bool isOutgoing)
            return isOutgoing ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        return HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>将未读计数转换为角标指示器的 Visibility</summary>
public class UnreadToBadgeVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>将 DateTime 转换为相对时间字符串（"2分钟前"、"1小时前"）</summary>
public class TimeAgoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return "";

        var span = DateTime.Now - dt;
        if (span.TotalSeconds < 60) return "now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dt.ToString("MM/dd");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>将布尔值转换为通话接听/拒接的前景色</summary>
public class BoolToCallColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isAccept)
            return isAccept ? "#22cc22" : "#cc2222";
        return "#999999";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
