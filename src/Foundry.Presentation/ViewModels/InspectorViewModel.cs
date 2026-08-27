using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Foundry.Application.Editing;
using Foundry.Application.Undo;
using Foundry.Presentation.ViewModels.Inspector;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Presentation.ViewModels;

/// <summary>
/// Construye el formulario de edicion de la entidad seleccionada reflexionando sobre su
/// <see cref="EditableSchema"/>. No conoce ningun tipo de entidad concreto. Toda edicion se
/// aplica a traves del <see cref="UndoStack"/>.
/// </summary>
public partial class InspectorViewModel : ObservableObject
{
    private readonly UndoStack _undoStack;

    [ObservableProperty]
    private bool _hasEntity;

    [ObservableProperty]
    private string? _entityTitle;

    public InspectorViewModel(UndoStack undoStack)
    {
        _undoStack = undoStack;
    }

    /// <summary>Se dispara cuando el usuario edita cualquier campo.</summary>
    public event EventHandler? EntityEdited;

    public ObservableCollection<FieldGroupViewModel> Groups { get; } = [];

    /// <summary>Entidades que referencian a la seleccionada ("¿quien usa esto?").</summary>
    public ObservableCollection<string> Usages { get; } = [];

    public bool HasUsages => Usages.Count > 0;

    public bool HasErrors => Fields().Any(field => field.HasErrors);

    public void Load(ContentEntity? entity, ContentDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        Groups.Clear();
        Usages.Clear();
        HasEntity = entity is not null;
        EntityTitle = entity is null ? null : $"{entity.CategoryName}  ·  {entity.Id}";

        if (entity is null)
        {
            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(HasUsages));
            return;
        }

        foreach (var link in ReferenceGraph.ReferrersOf(entity.Id, database))
        {
            Usages.Add($"{link.From.Name}  ·  {link.Field}");
        }

        OnPropertyChanged(nameof(HasUsages));

        void Apply(EditableField field, object? value)
        {
            _undoStack.Execute(new SetFieldValueAction(entity, field, value));
            EntityEdited?.Invoke(this, EventArgs.Empty);
        }

        foreach (var group in EditableSchema.For(entity.GetType()).ByGroup())
        {
            var fields = group
                .Select(field => CreateField(field, entity, database, Apply))
                .ToList();

            Groups.Add(new FieldGroupViewModel(group.Key, fields));
        }

        foreach (var field in Fields())
        {
            field.ErrorsChanged += OnFieldErrorsChanged;
            field.Revalidate();
        }

        OnPropertyChanged(nameof(HasErrors));
    }

    /// <summary>Tras undo/redo: los valores de la entidad cambiaron; refrescar los bindings.</summary>
    public void RefreshValues()
    {
        foreach (var field in Fields())
        {
            field.RefreshFromModel();
        }

        OnPropertyChanged(nameof(HasErrors));
    }

    private IEnumerable<PropertyFieldViewModel> Fields() => Groups.SelectMany(group => group.Fields);

    private void OnFieldErrorsChanged(object? sender, System.ComponentModel.DataErrorsChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasErrors));

    private static PropertyFieldViewModel CreateField(
        EditableField field,
        ContentEntity entity,
        ContentDatabase database,
        Action<EditableField, object?> apply) =>
        field.Kind switch
        {
            FieldKind.Toggle => new ToggleFieldViewModel(field, entity, apply),
            FieldKind.WholeNumber => new WholeNumberFieldViewModel(field, entity, apply),
            FieldKind.Number => new NumberFieldViewModel(field, entity, apply),
            FieldKind.Choice => new ChoiceFieldViewModel(field, entity, apply),
            FieldKind.Reference => new ReferenceFieldViewModel(field, entity, apply, database.All),
            _ => new TextFieldViewModel(field, entity, apply),
        };
}
