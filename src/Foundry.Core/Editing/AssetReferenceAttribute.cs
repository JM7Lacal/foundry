using Foundry.Core.Content;

namespace Foundry.Core.Editing;

/// <summary>
/// Indica que una propiedad <see cref="EntityId"/> (o string) referencia a otra entidad de
/// contenido de un tipo concreto. El Inspector muestra un selector en vez de un campo de texto,
/// y la validacion (Dia 3) verifica que la referencia resuelva.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class AssetReferenceAttribute : Attribute
{
    public AssetReferenceAttribute(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (!typeof(ContentEntity).IsAssignableFrom(targetType))
        {
            throw new ArgumentException(
                $"{targetType.Name} no es una entidad de contenido.", nameof(targetType));
        }

        TargetType = targetType;
    }

    /// <summary>Tipo de entidad al que apunta la referencia (por ejemplo <c>typeof(Enemy)</c>).</summary>
    public Type TargetType { get; }
}
