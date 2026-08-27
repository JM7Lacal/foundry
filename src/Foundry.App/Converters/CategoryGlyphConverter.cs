using System.Globalization;
using System.Windows.Data;

namespace Foundry.App.Converters;

/// <summary>
/// Nombre de categoria -> glifo de Segoe MDL2 / Fluent. Mantiene la eleccion de icono fuera del
/// ViewModel (que solo conoce CategoryName).
/// </summary>
public sealed class CategoryGlyphConverter : IValueConverter
{
    public static CategoryGlyphConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "Tropas" => "",    // People
            "Torres" => "",    // MapPin
            "Enemigos" => "",  // Bug
            _ => "",           // Folder
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
