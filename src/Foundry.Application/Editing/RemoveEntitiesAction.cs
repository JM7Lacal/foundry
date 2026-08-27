using Foundry.Application.Undo;
using Foundry.Core.Content;

namespace Foundry.Application.Editing;

/// <summary>Quita entidades de la base de forma reversible (el <c>Revert</c> las vuelve a poner).</summary>
public sealed class RemoveEntitiesAction : IUndoableAction
{
    private readonly ContentDatabase _database;
    private readonly IReadOnlyList<ContentEntity> _entities;

    public RemoveEntitiesAction(ContentDatabase database, IReadOnlyList<ContentEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(entities);

        _database = database;
        _entities = entities;
    }

    public string Description =>
        _entities.Count == 1 ? $"Eliminar «{_entities[0].Name}»" : $"Eliminar {_entities.Count} entidades";

    public void Apply()
    {
        foreach (var entity in _entities)
        {
            _database.Remove(entity.Id);
        }
    }

    public void Revert()
    {
        foreach (var entity in _entities)
        {
            if (!_database.Contains(entity.Id))
            {
                _database.Add(entity);
            }
        }
    }
}
