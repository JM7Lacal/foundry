namespace Foundry.Presentation.Services;

/// <summary>Tema visual de la aplicacion. La implementacion (WPF) intercambia el diccionario de paleta.</summary>
public interface IThemeService
{
    bool IsDark { get; }

    void Toggle();
}
