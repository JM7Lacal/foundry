using Foundry.Core.Content;

namespace Foundry.Application.Content;

/// <summary>
/// Puerto de persistencia de la base de contenido. La capa de casos de uso depende de esta
/// abstraccion; no sabe que la implementacion concreta usa JSON (ver Foundry.Infrastructure).
/// </summary>
public interface IContentRepository
{
    /// <summary>Carga la base de contenido desde <paramref name="path"/>.</summary>
    /// <exception cref="ContentRepositoryException">El archivo no existe o su contenido es invalido.</exception>
    Task<ContentDatabase> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Guarda la base completa en <paramref name="path"/> (sobrescribe).</summary>
    /// <exception cref="ContentRepositoryException">No se pudo escribir el archivo.</exception>
    Task SaveAsync(ContentDatabase database, string path, CancellationToken cancellationToken = default);
}
