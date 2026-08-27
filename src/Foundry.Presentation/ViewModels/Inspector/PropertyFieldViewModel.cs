using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Presentation.ViewModels.Inspector;

/// <summary>
/// Base de los editores de un campo del Inspector. El getter lee siempre de la entidad; el setter
/// delega en <see cref="Commit"/>, que aplica el cambio a traves del callback que pasa el
/// <see cref="InspectorViewModel"/> (hoy escribe directo; en el Dia 3 lo hara via undo/redo).
/// </summary>
public abstract class PropertyFieldViewModel : ObservableObject
{
    private readonly Action<EditableField, object?> _apply;

    protected PropertyFieldViewModel(EditableField field, ContentEntity entity, Action<EditableField, object?> apply)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(apply);

        Field = field;
        Entity = entity;
        _apply = apply;
    }

    public string Label => Field.Label;

    public string? Description => Field.Description;

    protected EditableField Field { get; }

    protected ContentEntity Entity { get; }

    protected object? CurrentValue => Field.GetValue(Entity);

    protected void Commit(object? value) => _apply(Field, value);
}

/// <summary>Cadena libre.</summary>
public sealed class TextFieldViewModel : PropertyFieldViewModel
{
    public TextFieldViewModel(EditableField field, ContentEntity entity, Action<EditableField, object?> apply)
        : base(field, entity, apply)
    {
    }

    public string Value
    {
        get => CurrentValue as string ?? string.Empty;
        set
        {
            Commit(value);
            OnPropertyChanged();
        }
    }
}

/// <summary>Numero entero, con deslizador cuando tiene rango.</summary>
public sealed class WholeNumberFieldViewModel : PropertyFieldViewModel
{
    public WholeNumberFieldViewModel(EditableField field, ContentEntity entity, Action<EditableField, object?> apply)
        : base(field, entity, apply)
    {
    }

    public int Value
    {
        get => Convert.ToInt32(CurrentValue ?? 0, CultureInfo.InvariantCulture);
        set
        {
            Commit(value);
            OnPropertyChanged();
        }
    }

    public bool HasRange => Field.HasRange;

    public double Minimum => Field.Minimum ?? 0;

    public double Maximum => Field.Maximum ?? 100;
}

/// <summary>Numero con decimales.</summary>
public sealed class NumberFieldViewModel : PropertyFieldViewModel
{
    public NumberFieldViewModel(EditableField field, ContentEntity entity, Action<EditableField, object?> apply)
        : base(field, entity, apply)
    {
    }

    public double Value
    {
        get => Convert.ToDouble(CurrentValue ?? 0d, CultureInfo.InvariantCulture);
        set
        {
            Commit(value);
            OnPropertyChanged();
        }
    }

    public bool HasRange => Field.HasRange;

    public double Minimum => Field.Minimum ?? 0;

    public double Maximum => Field.Maximum ?? 100;
}

/// <summary>Verdadero / falso.</summary>
public sealed class ToggleFieldViewModel : PropertyFieldViewModel
{
    public ToggleFieldViewModel(EditableField field, ContentEntity entity, Action<EditableField, object?> apply)
        : base(field, entity, apply)
    {
    }

    public bool Value
    {
        get => CurrentValue is true;
        set
        {
            Commit(value);
            OnPropertyChanged();
        }
    }
}

/// <summary>Un valor de un enum.</summary>
public sealed class ChoiceFieldViewModel : PropertyFieldViewModel
{
    public ChoiceFieldViewModel(EditableField field, ContentEntity entity, Action<EditableField, object?> apply)
        : base(field, entity, apply)
    {
        Options = Enum.GetValues(field.EnumType!).Cast<object>().ToArray();
    }

    public IReadOnlyList<object> Options { get; }

    public object? Value
    {
        get => CurrentValue;
        set
        {
            if (value is null)
            {
                return;
            }

            Commit(value);
            OnPropertyChanged();
        }
    }
}

/// <summary>Una opcion del selector de referencias (incluye la opcion "(ninguno)").</summary>
public sealed class ReferenceOption
{
    public ReferenceOption(EntityId? id, string label)
    {
        Id = id;
        Label = label;
    }

    public EntityId? Id { get; }

    public string Label { get; }
}

/// <summary>Referencia a otra entidad del tipo indicado por <see cref="AssetReferenceAttribute"/>.</summary>
public sealed class ReferenceFieldViewModel : PropertyFieldViewModel
{
    public ReferenceFieldViewModel(
        EditableField field,
        ContentEntity entity,
        Action<EditableField, object?> apply,
        IEnumerable<ContentEntity> candidates)
        : base(field, entity, apply)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var targetType = field.ReferenceTargetType!;
        var options = new List<ReferenceOption> { new(null, "(ninguno)") };
        options.AddRange(candidates
            .Where(candidate => targetType.IsInstanceOfType(candidate) && !ReferenceEquals(candidate, entity))
            .OrderBy(candidate => candidate.Name, StringComparer.CurrentCulture)
            .Select(candidate => new ReferenceOption(candidate.Id, $"{candidate.Name}  ·  {candidate.Id}")));

        Options = options;
    }

    public IReadOnlyList<ReferenceOption> Options { get; }

    public ReferenceOption? SelectedOption
    {
        get
        {
            var current = CurrentValue as EntityId?;
            return Options.FirstOrDefault(option => Nullable.Equals(option.Id, current)) ?? Options[0];
        }

        set
        {
            Commit(value?.Id);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBroken));
        }
    }

    /// <summary>La referencia actual apunta a un id que no existe entre las opciones.</summary>
    public bool IsBroken
    {
        get
        {
            var current = CurrentValue as EntityId?;
            return current is not null && !Options.Any(option => Nullable.Equals(option.Id, current));
        }
    }
}
