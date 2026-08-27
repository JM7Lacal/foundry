namespace Foundry.Core.Editing;

/// <summary>
/// Clasifica una propiedad editable para que el Inspector elija el control adecuado.
/// Se deriva del tipo de la propiedad y de sus atributos, no se declara a mano.
/// </summary>
public enum FieldKind
{
    /// <summary>Cadena libre — se edita con un cuadro de texto.</summary>
    Text,

    /// <summary>Numero entero — cuadro de texto y, si tiene rango, deslizador.</summary>
    WholeNumber,

    /// <summary>Numero con decimales.</summary>
    Number,

    /// <summary>Verdadero/falso — casilla.</summary>
    Toggle,

    /// <summary>Valor de un enum — lista desplegable.</summary>
    Choice,

    /// <summary>Referencia a otra entidad — selector de entidades del tipo indicado.</summary>
    Reference,
}
