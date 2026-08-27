using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Foundry.App.Converters;

/// <summary><c>true</c> (es error) → rojo; <c>false</c> (aviso) → ambar.</summary>
public sealed class IssueSeverityBrushConverter : IValueConverter
{
    public static IssueSeverityBrushConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is true ? "DangerBrush" : "WarnBrush";
        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
