using Foundry.Application.Content;
using Foundry.Core.Content;

namespace Foundry.Application.Editing;

/// <summary>
/// Convierte entidades a/desde texto JSON. El formato es un detalle de infraestructura; lo usan
/// el panel de preview y el asistente.
/// </summary>
public interface IContentSerializer
{
    /// <summary>Serializa una entidad (con su discriminador <c>$type</c>).</summary>
    string SerializeEntity(ContentEntity entity);

    /// <summary>
    /// Deserializa una o varias entidades desde un fragmento JSON: un objeto con <c>$type</c>,
    /// un array de esos objetos, o un objeto con una propiedad <c>entities</c> que sea ese array.
    /// </summary>
    /// <exception cref="ContentRepositoryException">El JSON no representa entidades validas.</exception>
    IReadOnlyList<ContentEntity> DeserializeEntities(string json);
}
