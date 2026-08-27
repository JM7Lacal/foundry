using System.IO;
using System.Text.Json;
using Foundry.Presentation.Services;

namespace Foundry.App.Services;

/// <summary>Guarda los ultimos archivos abiertos en <c>%APPDATA%\Foundry\recent.json</c>.</summary>
public sealed class JsonRecentFiles : IRecentFiles
{
    private const int Max = 8;

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Foundry", "recent.json");

    private readonly List<string> _items;

    public JsonRecentFiles()
    {
        _items = Read();
    }

    public IReadOnlyList<string> All => _items;

    public void Add(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var full = Path.GetFullPath(path);
        _items.RemoveAll(existing => string.Equals(existing, full, StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, full);

        if (_items.Count > Max)
        {
            _items.RemoveRange(Max, _items.Count - Max);
        }

        Write();
    }

    private static List<string> Read()
    {
        try
        {
            return File.Exists(StorePath)
                ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(StorePath)) ?? []
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
            File.WriteAllText(StorePath, JsonSerializer.Serialize(_items));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // preferencia best-effort; no vale la pena molestar al usuario
        }
    }
}
