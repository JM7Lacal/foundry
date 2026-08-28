using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Foundry.Presentation.Services;

namespace Foundry.App.Services;

/// <summary>Preferencias en <c>%APPDATA%\Foundry\preferences.json</c>. Best-effort: si falla, usa defaults.</summary>
public sealed class JsonPreferences : IPreferences
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Foundry", "preferences.json");

    private readonly Dictionary<string, bool> _values;

    public JsonPreferences()
    {
        _values = Read();
    }

    public bool GetBool(string key, bool fallback) => _values.TryGetValue(key, out var value) ? value : fallback;

    public void SetBool(string key, bool value)
    {
        _values[key] = value;
        Write();
    }

    private static Dictionary<string, bool> Read()
    {
        try
        {
            return File.Exists(StorePath)
                ? JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(StorePath)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Write()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(_values));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best-effort
        }
    }
}
