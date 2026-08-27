using Foundry.Application.Undo;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Application.Editing;

/// <summary>
/// Cambio reversible del valor de un campo de una entidad. Todas las ediciones del Inspector
/// pasan por una de estas y por el <see cref="UndoStack"/>: nunca se muta la entidad directo.
/// </summary>
public sealed class SetFieldValueAction : IUndoableAction
{
    private readonly ContentEntity _entity;
    private readonly EditableField _field;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    public SetFieldValueAction(ContentEntity entity, EditableField field, object? newValue)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(field);

        _entity = entity;
        _field = field;
        _oldValue = field.GetValue(entity);
        _newValue = newValue;
    }

    public string Description => $"Cambiar «{_field.Label}» de {_entity.Name}";

    public void Apply() => _field.SetValue(_entity, _newValue);

    public void Revert() => _field.SetValue(_entity, _oldValue);
}
