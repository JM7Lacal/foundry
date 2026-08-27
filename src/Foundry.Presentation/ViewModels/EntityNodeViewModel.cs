using CommunityToolkit.Mvvm.ComponentModel;
using Foundry.Core.Content;

namespace Foundry.Presentation.ViewModels;

/// <summary>Nodo hoja del arbol de contenido: envuelve una entidad.</summary>
public sealed class EntityNodeViewModel : ObservableObject
{
    public EntityNodeViewModel(ContentEntity entity)
    {
        Entity = entity;
    }

    public ContentEntity Entity { get; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Entity.Name) ? Entity.Id.Value : Entity.Name;

    /// <summary>Fuerza al arbol a releer <see cref="DisplayName"/> tras editar el nombre en el Inspector.</summary>
    public void Refresh() => OnPropertyChanged(nameof(DisplayName));
}
