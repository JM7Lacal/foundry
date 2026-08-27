namespace Foundry.Core.Content;

/// <summary>
/// Identificador estable de una pieza de contenido de juego (por ejemplo "troop.archer").
/// No vacio y sin espacios en los bordes. Es un value object: dos <see cref="EntityId"/> con
/// el mismo texto son iguales.
/// </summary>
public readonly record struct EntityId
{
    public EntityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El id de una entidad no puede estar vacio.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
