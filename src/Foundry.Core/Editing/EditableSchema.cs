using System.Collections.Concurrent;
using System.Reflection;
using Foundry.Core.Content;

namespace Foundry.Core.Editing;

/// <summary>
/// La lista de <see cref="EditableField"/> de un tipo de entidad, en orden de presentacion.
/// Se calcula una vez por tipo (reflexion) y se cachea. Es lo que hace que agregar una entidad
/// nueva no requiera escribir UI: basta con anotar sus propiedades.
/// </summary>
public sealed class EditableSchema
{
    private static readonly ConcurrentDictionary<Type, EditableSchema> Cache = new();

    private EditableSchema(Type entityType, IReadOnlyList<EditableField> fields)
    {
        EntityType = entityType;
        Fields = fields;
    }

    public Type EntityType { get; }

    public IReadOnlyList<EditableField> Fields { get; }

    /// <summary>Campos agrupados por <see cref="EditableField.Group"/>, preservando el orden.</summary>
    public IEnumerable<IGrouping<string, EditableField>> ByGroup() => Fields.GroupBy(field => field.Group);

    public static EditableSchema For(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (!typeof(ContentEntity).IsAssignableFrom(entityType))
        {
            throw new ArgumentException($"{entityType.Name} no es una entidad de contenido.", nameof(entityType));
        }

        return Cache.GetOrAdd(entityType, Build);
    }

    private static EditableSchema Build(Type entityType)
    {
        var fields = entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (property, attribute: property.GetCustomAttribute<EditablePropertyAttribute>()))
            .Where(pair => pair.attribute is not null)
            .Select(pair => new EditableField(pair.property, pair.attribute!))
            .OrderBy(field => field.Order)
            .ThenBy(field => field.Label, StringComparer.CurrentCulture)
            .ToList();

        return new EditableSchema(entityType, fields);
    }
}
