using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Foundry.Presentation.ViewModels;

/// <summary>Nodo de categoria del arbol ("Tropas", "Torres", "Enemigos").</summary>
public sealed partial class ContentCategoryViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIssue))]
    [NotifyPropertyChangedFor(nameof(IsErrorBadge))]
    [NotifyPropertyChangedFor(nameof(IsWarningBadge))]
    private NodeBadge _badge;

    /// <summary>Resumen de problemas de la categoria, para el tooltip del badge.</summary>
    [ObservableProperty]
    private string? _badgeTooltip;

    public ContentCategoryViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public ObservableCollection<EntityNodeViewModel> Entities { get; } = [];

    public bool HasIssue => Badge != NodeBadge.None;

    public bool IsErrorBadge => Badge == NodeBadge.Error;

    public bool IsWarningBadge => Badge == NodeBadge.Warning;
}
