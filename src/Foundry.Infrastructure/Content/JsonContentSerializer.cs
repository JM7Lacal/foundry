using System.Text.Json;
using Foundry.Application.Content;
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

    public IReadOnlyList<ContentEntity> DeserializeEntities(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var payload = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("entities", out var array)
                ? array
                : root;

            var entities = new List<ContentEntity>();

            switch (payload.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var element in payload.EnumerateArray())
                    {
                        entities.Add(ReadEntity(element));
                    }

                    break;

                case JsonValueKind.Object:
                    entities.Add(ReadEntity(payload));
                    break;

                default:
                    throw new ContentRepositoryException("Se esperaba un objeto o array de entidades.");
            }

            return entities;
        }
        catch (JsonException ex)
        {
            throw new ContentRepositoryException($"JSON de entidades invalido: {ex.Message}", ex);
        }
        catch (ArgumentException ex)
        {
            throw new ContentRepositoryException(ex.Message, ex);
        }
    }

    private ContentEntity ReadEntity(JsonElement element) =>
        element.Deserialize<ContentEntity>(_options)
        ?? throw new ContentRepositoryException("Se encontro una entidad nula.");
}
