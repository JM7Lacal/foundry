using System.Collections;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Presentation.ViewModels.Inspector;

/// <summary>
/// Base de los editores de un campo del Inspector.
/// <para>
/// El getter lee siempre de la entidad. El setter delega en <see cref="Commit"/>, que aplica el
/// cambio via el callback del <see cref="InspectorViewModel"/> (que lo encola en el
/// <c>UndoStack</c>).
/// </para>
/// <para>
/// Implementa <see cref="INotifyDataErrorInfo"/>: cada subtipo aporta su regla en
/// <see cref="Validate"/> (rango, requerido, referencia rota).
/// </para>
/// </summary>
public abstract class PropertyFieldViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly Action<EditableField, object?> _apply;
    private string? _error;

    protected PropertyFieldViewModel(EditableField field, ContentEntity entity, Action<EditableField, object?> apply)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(apply);

        Field = field;
        Entity = entity;
        _apply = apply;
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public string Label => Field.Label;

    public string? Description => Field.Description;

    public bool IsRequired => Field.IsRequired;

    public string RequiredMark => Field.IsRequired ? " *" : string.Empty;

    public bool HasErrors => _error is not null;

    public string? ErrorText => _error;

    protected EditableField Field { get; }

    protected ContentEntity Entity { get; }

    protected object? CurrentValue => Field.GetValue(Entity);

    /// <summary>Nombre de la propiedad de valor del subtipo, para <see cref="INotifyDataErrorInfo"/>.</summary>
    protected abstract string ValuePropertyName { get; }

    public IEnumerable GetErrors(string? propertyName) =>
        _error is null ? Array.Empty<string>() : new[] { _error };

    /// <summary>Recalcula la validacion. Llamar al construir el campo y tras cada cambio externo.</summary>
    public void Revalidate()
    {
        var next = Validate();
        if (next == _error)
        {
            return;
        }

        _error = next;
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(ValuePropertyName));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ErrorText));
    }

    /// <summary>La entidad cambio por afuera (undo/redo): releer todos los bindings y revalidar.</summary>
    public void RefreshFromModel()
    {
        OnPropertyChanged(string.Empty);
        Revalidate();
    }

    protected void Commit(object? value)
    {
        _apply(Field, value);
        Revalidate();
    }

    protected virtual string? Validate()
    {
        if (Field.IsRequired && IsEmpty(CurrentValue))
        {
            return "Requerido.";
        }

        return null;
    }

    protected static bool IsEmpty(object? value) =>
        value is null || (value is string text && string.IsNullOrWhiteSpace(text));

    protected string RangeError() =>
        $"Debe estar entre {Field.Minimum?.ToString("0.###", CultureInfo.CurrentCulture)} "
        + $"y {Field.Maximum?.ToString("0.###", CultureInfo.CurrentCulture)}.";
}

/// <summary>Cadena libre.</summary>
public sealed class TextFieldViewModel : PropertyFieldViewModel
{
    public TextFieldViewModel(EditableField field, ContentEntity entity, Action<EditableField, object?> apply)
        : base(field, entity, apply)
    {
    }

    protected override string ValuePropertyName => nameof(Value);

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

    protected override string ValuePropertyName => nameof(Value);

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

    protected override string? Validate() =>
        base.Validate()
        ?? (Field.HasRange && (Value < Field.Minimum || Value > Field.Maximum) ? RangeError() : null);
}

/// <summary>Numero con decimales.</summary>
public sealed class NumberFieldViewModel : PropertyFieldViewModel
{
    public NumberFieldViewModel(EditableField field, ContentEntity entity, Action<EditableField, object?> apply)
        : base(field, entity, apply)
    {
    }

    protected override string ValuePropertyName => nameof(Value);

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

    protected override string? Validate() =>
        base.Validate()
        ?? (Field.HasRange && (Value < Field.Minimum || Value > Field.Maximum) ? RangeError() : null);
}

/// <summary>Verdadero / falso.</summary>
public sealed class ToggleFieldViewModel : PropertyFieldViewModel
{
    public ToggleFieldViewModel(EditableField field, ContentEntity entity, Action<EditableField, object?> apply)
        : base(field, entity, apply)
    {
    }

    protected override string ValuePropertyName => nameof(Value);

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

    protected override string ValuePropertyName => nameof(Value);

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

    // Respaldo por si un ComboBox no aplica el DisplayMemberPath en el cuadro de seleccion.
    public override string ToString() => Label;
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

    protected override string ValuePropertyName => nameof(SelectedOption);

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

    protected override string? Validate() => IsBroken ? "La referencia no existe." : null;
}
