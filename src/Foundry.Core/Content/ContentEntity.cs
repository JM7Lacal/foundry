using Foundry.Core.Editing;

namespace Foundry.Core.Content;

/// <summary>
/// Base de toda pieza de contenido editable (tropas, torres, enemigos, ...).
/// Las propiedades marcadas con <see cref="EditablePropertyAttribute"/> son las que el Inspector
/// expone. Los tipos concretos solo agregan propiedades anotadas: no escriben UI.
/// </summary>
/// <remarks>
/// <see cref="Id"/> se asigna al crear la entidad (por el editor o al deserializar). Una entidad
/// con <c>Id</c> sin asignar no puede entrar a un <see cref="ContentDatabase"/>.
/// </remarks>
public abstract class ContentEntity
{
    /// <summary>Identificador estable y unico dentro de la base de contenido.</summary>
    public EntityId Id { get; set; }

    [EditableProperty(Label = "Nombre", Group = "General", Order = 0)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Etiqueta legible del tipo, para agrupar en el arbol de contenido ("Tropas", ...).</summary>
    public abstract string CategoryName { get; }

    public override string ToString() => $"{CategoryName}: {Name} ({Id})";
}
