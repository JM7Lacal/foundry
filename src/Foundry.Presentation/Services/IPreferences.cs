namespace Foundry.Presentation.Services;

/// <summary>Preferencias de usuario persistidas entre sesiones (tema, autoguardado, ...).</summary>
public interface IPreferences
{
    bool GetBool(string key, bool fallback);

    void SetBool(string key, bool value);
}
