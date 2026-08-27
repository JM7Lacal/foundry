namespace Foundry.Core.Content;

/// <summary>
/// El conjunto de todo el contenido cargado, indexado por <see cref="EntityId"/>.
/// Es el agregado sobre el que trabaja el editor: el arbol lo recorre, el Inspector edita sus
/// entidades y el repositorio lo serializa entero.
/// </summary>
public sealed class ContentDatabase
{
    private readonly Dictionary<EntityId, ContentEntity> _byId = [];

    /// <summary>Todas las entidades, sin orden garantizado.</summary>
    public IReadOnlyCollection<ContentEntity> All => _byId.Values;

    public int Count => _byId.Count;

    /// <summary>Agrega una entidad. Lanza si no tiene id o si el id ya existe.</summary>
    public void Add(ContentEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (string.IsNullOrEmpty(entity.Id.Value))
        {
            throw new ArgumentException("La entidad no tiene un id asignado.", nameof(entity));
        }

        if (!_byId.TryAdd(entity.Id, entity))
        {
            throw new ArgumentException($"Ya existe una entidad con id '{entity.Id}'.", nameof(entity));
        }
    }

    public bool Remove(EntityId id) => _byId.Remove(id);

    public bool Contains(EntityId id) => _byId.ContainsKey(id);

    /// <summary>Devuelve la entidad, o <c>null</c> si no esta.</summary>
    public ContentEntity? Find(EntityId id) => _byId.GetValueOrDefault(id);

    /// <summary>Devuelve la entidad o lanza <see cref="KeyNotFoundException"/>.</summary>
    public ContentEntity Get(EntityId id) =>
        Find(id) ?? throw new KeyNotFoundException($"No hay ninguna entidad con id '{id}'.");

    /// <summary>Todas las entidades de un tipo concreto (por ejemplo <c>OfType&lt;Troop&gt;()</c>).</summary>
    public IEnumerable<T> OfType<T>() where T : ContentEntity => _byId.Values.OfType<T>();

    /// <summary>Ids que aparecen referenciados pero no existen en la base (referencias rotas).</summary>
    public bool IsBrokenReference(EntityId? reference) =>
        reference.HasValue && !Contains(reference.Value);
}
