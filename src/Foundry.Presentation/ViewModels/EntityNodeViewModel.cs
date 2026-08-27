using CommunityToolkit.Mvvm.ComponentModel;
using Foundry.Core.Content;

namespace Foundry.Presentation.ViewModels;

/// <summary>Nodo hoja del arbol de contenido: envuelve una entidad.</summary>
public sealed partial class EntityNodeViewModel : ObservableObject
{
    /// <summary>Enlazado a <c>TreeViewItem.IsSelected</c> (dos vias) para poder seleccionar desde el ViewModel.</summary>
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIssue))]
    [NotifyPropertyChangedFor(nameof(IsErrorBadge))]
    private NodeBadge _badge;

    public EntityNodeViewModel(ContentEntity entity)
    {
        Entity = entity;
    }

    public ContentEntity Entity { get; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Entity.Name) ? Entity.Id.Value : Entity.Name;

    public bool HasIssue => Badge != NodeBadge.None;

    public bool IsErrorBadge => Badge == NodeBadge.Error;

    /// <summary>Fuerza al arbol a releer <see cref="DisplayName"/> tras editar el nombre en el Inspector.</summary>
    public void Refresh() => OnPropertyChanged(nameof(DisplayName));
}
