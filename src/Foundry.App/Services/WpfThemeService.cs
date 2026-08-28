using System.Linq;
using System.Windows;
using Foundry.Presentation.Services;

namespace Foundry.App.Services;

/// <summary>
/// Cambia el tema intercambiando el <see cref="ResourceDictionary"/> de paleta en los recursos de
/// la aplicacion. Los estilos referencian los brushes con <c>DynamicResource</c>, asi que el cambio
/// se propaga sin reiniciar. La preferencia se guarda via <see cref="IPreferences"/>.
/// </summary>
public sealed class WpfThemeService : IThemeService
{
    private const string PreferenceKey = "theme.dark";

    private readonly IPreferences _preferences;

    public WpfThemeService(IPreferences preferences)
    {
        _preferences = preferences;
        IsDark = preferences.GetBool(PreferenceKey, false);
        ApplyPalette(IsDark);
    }

    public bool IsDark { get; private set; }

    public void Toggle()
    {
        IsDark = !IsDark;
        ApplyPalette(IsDark);
        _preferences.SetBool(PreferenceKey, IsDark);
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
}
