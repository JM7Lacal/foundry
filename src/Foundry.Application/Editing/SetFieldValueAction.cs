using Foundry.Application.Undo;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Application.Editing;

/// <summary>
/// Cambio reversible del valor de un campo de una entidad. Todas las ediciones del Inspector
/// pasan por una de estas y por el <see cref="UndoStack"/>: nunca se muta la entidad directo.
/// </summary>
/// <remarks>
/// Las ediciones en rafaga sobre el mismo campo (arrastrar un deslizador) se fusionan en el
/// historial mientras no pase mucho tiempo entre una y la siguiente: el <c>Undo</c> vuelve al
/// valor previo al gesto, no a cada paso intermedio.
/// </remarks>
public sealed class SetFieldValueAction : IUndoableAction
{
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(700);

    private readonly ContentEntity _entity;
    private readonly EditableField _field;
    private readonly object? _oldValue;

    private object? _newValue;
    private DateTime _lastEditUtc;

    public SetFieldValueAction(ContentEntity entity, EditableField field, object? newValue)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(field);

        _entity = entity;
        _field = field;
        _oldValue = field.GetValue(entity);
        _newValue = newValue;
        _lastEditUtc = DateTime.UtcNow;
    }

    public string Description => $"Cambiar «{_field.Label}» de {_entity.Name}";

    public void Apply() => _field.SetValue(_entity, _newValue);

    public void Revert() => _field.SetValue(_entity, _oldValue);

    public bool TryCoalesceWith(IUndoableAction newer)
    {
        if (newer is not SetFieldValueAction other
            || !ReferenceEquals(other._entity, _entity)
            || !ReferenceEquals(other._field, _field)
            || other._lastEditUtc - _lastEditUtc > CoalesceWindow)
        {
            return false;
        }

        _newValue = other._newValue;
        _lastEditUtc = other._lastEditUtc;
        return true;
    }
}
