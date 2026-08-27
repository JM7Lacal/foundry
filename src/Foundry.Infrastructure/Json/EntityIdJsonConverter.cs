using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Core.Content;

namespace Foundry.Infrastructure.Json;

/// <summary>
/// Serializa <see cref="EntityId"/> como una cadena JSON simple ("troop.archer") en vez de un
/// objeto. Mantiene <see cref="EntityId"/> libre de atributos de serializacion: el dominio no
/// sabe que se persiste en JSON.
/// </summary>
public sealed class EntityIdJsonConverter : JsonConverter<EntityId>
{
    public override EntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Se esperaba un id de entidad no vacio.");
        }

        return new EntityId(value);
    }

    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }

    public override EntityId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? throw new JsonException("Nombre de propiedad vacio."));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(value.Value);
    }
}
