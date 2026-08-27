using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Foundry.Presentation.ViewModels;

/// <summary>Nodo de categoria del arbol ("Tropas", "Torres", "Enemigos").</summary>
public sealed partial class ContentCategoryViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isSelected;

    public ContentCategoryViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public ObservableCollection<EntityNodeViewModel> Entities { get; } = [];
}
