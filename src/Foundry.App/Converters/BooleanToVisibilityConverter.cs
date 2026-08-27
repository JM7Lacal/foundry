using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Foundry.App.Converters;

/// <summary>
/// <see cref="bool"/> → <see cref="Visibility"/>, con dos instancias listas para <c>{x:Static}</c>.
/// (El converter propio de WPF no tiene variante invertida.)
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public static BooleanToVisibilityConverter VisibleWhenTrue { get; } = new();

    public static BooleanToVisibilityConverter VisibleWhenFalse { get; } = new() { Invert = true };

    public bool Invert { get; init; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
