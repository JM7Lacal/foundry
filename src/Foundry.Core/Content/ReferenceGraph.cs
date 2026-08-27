using Foundry.Core.Editing;

namespace Foundry.Core.Content;

/// <summary>Una arista del grafo de referencias: quien referencia a quien, por que campo.</summary>
public readonly record struct ReferenceLink(ContentEntity From, string Field, EntityId Target);

/// <summary>
/// Consultas sobre las referencias entre entidades (campos <see cref="FieldKind.Reference"/>).
/// Sirve para "¿quien usa esto?" antes de borrar o renombrar, y para listar referencias rotas.
/// </summary>
public static class ReferenceGraph
{
    /// <summary>Todas las referencias que salen de una entidad.</summary>
    public static IEnumerable<ReferenceLink> LinksFrom(ContentEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        foreach (var field in EditableSchema.For(entity.GetType()).Fields)
        {
            if (field.Kind == FieldKind.Reference && field.GetValue(entity) is EntityId target)
            {
                yield return new ReferenceLink(entity, field.Label, target);
            }
        }
    }

    /// <summary>Entidades que referencian a <paramref name="target"/>.</summary>
    public static IReadOnlyList<ReferenceLink> ReferrersOf(EntityId target, ContentDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        return database.All
            .SelectMany(LinksFrom)
            .Where(link => link.Target.Equals(target))
            .ToList();
    }

    /// <summary>Referencias que apuntan a un id inexistente en la base.</summary>
    public static IReadOnlyList<ReferenceLink> BrokenLinks(ContentDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        return database.All
            .SelectMany(LinksFrom)
            .Where(link => !database.Contains(link.Target))
            .ToList();
    }
}
