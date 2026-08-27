using System.IO;
using System.Linq;
using System.Windows;
using Foundry.Presentation.Services;

namespace Foundry.App.Services;

/// <summary>
/// Cambia el tema intercambiando el <see cref="ResourceDictionary"/> de paleta en los recursos de
/// la aplicacion. Los estilos referencian los brushes con <c>DynamicResource</c>, asi que el cambio
/// se propaga sin reiniciar. La preferencia se guarda en <c>%APPDATA%\Foundry\theme.txt</c>.
/// </summary>
public sealed class WpfThemeService : IThemeService
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Foundry", "theme.txt");

    public WpfThemeService()
    {
        IsDark = ReadPreference();
        ApplyPalette(IsDark);
    }

    public bool IsDark { get; private set; }

    public void Toggle()
    {
        IsDark = !IsDark;
        ApplyPalette(IsDark);
        WritePreference(IsDark);
    }

    private static void ApplyPalette(bool dark)
    {
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;

        var current = dictionaries.FirstOrDefault(
            dictionary => dictionary.Source?.OriginalString.Contains("Palette", StringComparison.Ordinal) == true);

        var next = new ResourceDictionary
        {
            Source = new Uri($"Themes/Palette.{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative),
        };

        if (current is not null)
        {
            dictionaries.Remove(current);
        }

        dictionaries.Add(next);
    }

    private static bool ReadPreference()
    {
        try
        {
            return File.Exists(PreferencePath)
                   && File.ReadAllText(PreferencePath).Trim().Equals("dark", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void WritePreference(bool dark)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
            File.WriteAllText(PreferencePath, dark ? "dark" : "light");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best-effort
        }
    }
}
