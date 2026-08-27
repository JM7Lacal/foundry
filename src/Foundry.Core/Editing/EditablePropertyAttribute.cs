namespace Foundry.Core.Editing;

/// <summary>
/// Marca una propiedad de una entidad de contenido como editable en el Inspector.
/// El Inspector (Dia 2) reflexiona sobre estos atributos para construir la UI:
/// no hay un formulario escrito a mano por tipo.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class EditablePropertyAttribute : Attribute
{
    /// <summary>Etiqueta visible. Si es null, el Inspector usa el nombre de la propiedad.</summary>
    public string? Label { get; init; }

    /// <summary>Grupo/seccion donde se agrupa el campo (por ejemplo "Combate", "Economia").</summary>
    public string Group { get; init; } = "General";

    /// <summary>Orden relativo dentro del grupo. Menor primero.</summary>
    public int Order { get; init; }

    /// <summary>Texto de ayuda opcional (tooltip).</summary>
    public string? Description { get; init; }
}
