using System.Text.Json;
using Foundry.Application.Editing;
using Foundry.Core.Content;
using Foundry.Infrastructure.Json;

namespace Foundry.Infrastructure.Content;

/// <summary>
/// <see cref="IContentSerializer"/> con las mismas <see cref="JsonSerializerOptions"/> que el
/// repositorio: lo que muestra el preview es exactamente lo que se guardaria en disco.
/// </summary>
public sealed class JsonContentSerializer : IContentSerializer
{
    private readonly JsonSerializerOptions _options = FoundryJsonOptions.Create();

    public string SerializeEntity(ContentEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Declarar el tipo estatico ContentEntity hace que aparezca el discriminador "$type".
        ContentEntity asBase = entity;
        return JsonSerializer.Serialize(asBase, _options);
    }
}
