namespace Foundry.Presentation.ViewModels.Inspector;

/// <summary>Un grupo de campos del Inspector ("Combate", "Economia", ...).</summary>
public sealed class FieldGroupViewModel
{
    public FieldGroupViewModel(string name, IReadOnlyList<PropertyFieldViewModel> fields)
    {
        Name = name;
        Fields = fields;
    }

    public string Name { get; }

    public IReadOnlyList<PropertyFieldViewModel> Fields { get; }
}
