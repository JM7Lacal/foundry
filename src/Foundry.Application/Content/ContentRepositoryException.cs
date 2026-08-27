namespace Foundry.Application.Content;

/// <summary>
/// Falla al cargar o guardar la base de contenido: archivo inexistente, JSON malformado,
/// ids duplicados, tipo de entidad desconocido, error de IO.
/// </summary>
public sealed class ContentRepositoryException : Exception
{
    public ContentRepositoryException()
    {
    }

    public ContentRepositoryException(string message)
        : base(message)
    {
    }

    public ContentRepositoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
