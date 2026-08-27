using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Foundry.Presentation.ViewModels.Inspector;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Presentation.ViewModels;

/// <summary>
/// Construye el formulario de edicion de la entidad seleccionada reflexionando sobre su
/// <see cref="EditableSchema"/>. No conoce ningun tipo de entidad concreto: agregar una entidad
/// nueva no toca esta clase.
/// </summary>
public partial class InspectorViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _hasEntity;

    [ObservableProperty]
    private string? _entityTitle;

    public ObservableCollection<FieldGroupViewModel> Groups { get; } = [];

    /// <summary>Se dispara cuando el usuario edita cualquier campo.</summary>
    public event EventHandler? EntityEdited;

    public void Load(ContentEntity? entity, ContentDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        Groups.Clear();
        HasEntity = entity is not null;
        EntityTitle = entity is null ? null : $"{entity.CategoryName}  ·  {entity.Id}";

        if (entity is null)
        {
            return;
        }

        void Apply(EditableField field, object? value)
        {
            field.SetValue(entity, value);
            EntityEdited?.Invoke(this, EventArgs.Empty);
        }

        foreach (var group in EditableSchema.For(entity.GetType()).ByGroup())
        {
            var fields = group
                .Select(field => CreateField(field, entity, database, Apply))
                .ToList();

            Groups.Add(new FieldGroupViewModel(group.Key, fields));
        }
    }

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
