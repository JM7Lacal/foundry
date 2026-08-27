using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Foundry.App.Converters;

/// <summary>
/// Convierte "hay valor / no hay valor" en <see cref="Visibility"/>. Se exponen dos instancias
/// listas para usar desde XAML con <c>{x:Static}</c>.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <summary>Colapsa el elemento cuando el valor es <c>null</c>.</summary>
    public static NullToVisibilityConverter CollapsedWhenNull { get; } = new();

    /// <summary>Colapsa el elemento cuando el valor NO es <c>null</c>.</summary>
    public static NullToVisibilityConverter CollapsedWhenNotNull { get; } = new() { Invert = true };

    public bool Invert { get; init; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isNull = value is null;
        if (Invert)
        {
            isNull = !isNull;
        }

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
