using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Foundry.App.Converters;

/// <summary>Visible cuando el string tiene contenido; colapsado si es null o vacio.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public static StringToVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
