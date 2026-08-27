using System.Collections.ObjectModel;

namespace Foundry.Presentation.ViewModels;

/// <summary>Nodo de categoria del arbol ("Tropas", "Torres", "Enemigos").</summary>
public sealed class ContentCategoryViewModel
{
    public ContentCategoryViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public ObservableCollection<EntityNodeViewModel> Entities { get; } = [];
}
