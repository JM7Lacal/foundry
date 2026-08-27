using Foundry.Core.Content;

namespace Foundry.Application.Editing;

/// <summary>Serializa una entidad a texto para el panel de preview. Formato = detalle de infraestructura.</summary>
public interface IContentSerializer
{
    string SerializeEntity(ContentEntity entity);
}
