using Foundry.Application.Undo;
using Foundry.Core.Content;

namespace Foundry.Application.Editing;

/// <summary>
/// Agrega (o reemplaza por id) un conjunto de entidades a la base, de forma reversible.
/// Lo usa el asistente al aplicar una propuesta.
/// </summary>
public sealed class AddEntitiesAction : IUndoableAction
{
    private readonly ContentDatabase _database;
    private readonly IReadOnlyList<ContentEntity> _entities;
    private readonly List<(EntityId Id, ContentEntity? Previous)> _replaced = [];

    public AddEntitiesAction(ContentDatabase database, IReadOnlyList<ContentEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(entities);

        _database = database;
        _entities = entities;
    }

    public string Description =>
        _entities.Count == 1 ? $"Agregar «{_entities[0].Name}»" : $"Agregar {_entities.Count} entidades";

    public void Apply()
    {
        _replaced.Clear();

        foreach (var entity in _entities)
        {
            _replaced.Add((entity.Id, _database.Find(entity.Id)));
            _database.Remove(entity.Id);
            _database.Add(entity);
        }
    }

    public void Revert()
    {
        for (var i = _replaced.Count - 1; i >= 0; i--)
        {
            var (id, previous) = _replaced[i];
            _database.Remove(id);
            if (previous is not null)
            {
                _database.Add(previous);
            }
        }

        _replaced.Clear();
    }
}
