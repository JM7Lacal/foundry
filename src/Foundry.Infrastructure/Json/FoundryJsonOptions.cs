using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Foundry.Core.Content;

namespace Foundry.Infrastructure.Json;

/// <summary>
/// Construye las <see cref="JsonSerializerOptions"/> que usa el repositorio: nombres camelCase,
/// enums como texto, <see cref="EntityId"/> como string y polimorfismo de
/// <see cref="ContentEntity"/> por discriminador <c>$type</c>.
/// </summary>
/// <remarks>
/// Los subtipos de <see cref="ContentEntity"/> se descubren por reflexion sobre el ensamblado del
/// dominio. Agregar una entidad nueva (una clase mas) no requiere tocar la serializacion.
/// </remarks>
public static class FoundryJsonOptions
{
    private static readonly ReadOnlyCollection<Type> EntityTypes = new(
        typeof(ContentEntity).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && t.IsSubclassOf(typeof(ContentEntity)))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList());

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { AddContentEntityPolymorphism },
            },
        };

        options.Converters.Add(new EntityIdJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }

    /// <summary>Discriminador estable para un subtipo de entidad ("Troop" -> "troop").</summary>
    public static string DiscriminatorFor(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return entityType.Name.ToLowerInvariant();
    }

    private static void AddContentEntityPolymorphism(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(ContentEntity))
        {
            return;
        }

        var polymorphism = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$type",
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };

        foreach (var entityType in EntityTypes)
        {
            polymorphism.DerivedTypes.Add(new JsonDerivedType(entityType, DiscriminatorFor(entityType)));
        }

        typeInfo.PolymorphismOptions = polymorphism;
    }
}
