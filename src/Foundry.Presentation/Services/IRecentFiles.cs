namespace Foundry.Presentation.Services;

/// <summary>Lista de archivos abiertos recientemente, persistida entre sesiones.</summary>
public interface IRecentFiles
{
    IReadOnlyList<string> All { get; }

    void Add(string path);
}
