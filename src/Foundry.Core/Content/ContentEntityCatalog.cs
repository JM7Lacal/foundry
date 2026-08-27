using System.Collections.ObjectModel;

namespace Foundry.Core.Content;

/// <summary>
/// Descubre por reflexion todos los tipos concretos de <see cref="ContentEntity"/> del dominio y
/// les asigna un discriminador estable (el nombre del tipo en minuscula). Una sola fuente de
/// verdad para la serializacion JSON y para los importadores.
/// </summary>
public static class ContentEntityCatalog
{
    public static IReadOnlyList<Type> Types { get; } = new ReadOnlyCollection<Type>(
        typeof(ContentEntity).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } && type.IsSubclassOf(typeof(ContentEntity)))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList());

    public static string DiscriminatorFor(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return entityType.Name.ToLowerInvariant();
    }

    /// <summary>Devuelve el tipo cuyo discriminador coincide (sin distinguir mayusculas), o <c>null</c>.</summary>
    public static Type? Resolve(string discriminator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discriminator);
        var normalized = discriminator.Trim().ToLowerInvariant();
        return Types.FirstOrDefault(type => DiscriminatorFor(type) == normalized);
    }
}
