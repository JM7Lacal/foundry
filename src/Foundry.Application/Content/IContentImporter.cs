using Foundry.Core.Content;

namespace Foundry.Application.Content;

/// <summary>
/// Trae entidades desde un formato externo (CSV, planilla, formato propio del engine).
/// El editor solo conoce esta abstraccion; agregar un formato nuevo es una clase mas.
/// </summary>
public interface IContentImporter
{
    /// <summary>Nombre corto del formato, para menus y mensajes ("CSV").</summary>
    string FormatName { get; }

    /// <summary>Filtro para el dialogo de archivo ("Planillas CSV (*.csv)|*.csv").</summary>
    string FileFilter { get; }

    /// <exception cref="ContentImportException">El archivo no existe o su contenido no se pudo interpretar.</exception>
    IReadOnlyList<ContentEntity> Import(string path);
}

/// <summary>Falla al importar: cabecera invalida, tipo desconocido, valor que no castea.</summary>
public sealed class ContentImportException : Exception
{
    public ContentImportException()
    {
    }

    public ContentImportException(string message)
        : base(message)
    {
    }

    public ContentImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
