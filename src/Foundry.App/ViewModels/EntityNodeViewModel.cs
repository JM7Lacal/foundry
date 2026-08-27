using Foundry.Core.Content;

namespace Foundry.App.ViewModels;

/// <summary>Nodo hoja del arbol de contenido: envuelve una entidad.</summary>
public sealed class EntityNodeViewModel
{
    public EntityNodeViewModel(ContentEntity entity)
    {
        Entity = entity;
    }

    public ContentEntity Entity { get; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Entity.Name) ? Entity.Id.Value : Entity.Name;
}
