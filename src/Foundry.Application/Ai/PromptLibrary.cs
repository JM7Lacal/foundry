using System.Reflection;

namespace Foundry.Application.Ai;

/// <summary>
/// El catalogo de system prompts disponibles. Los textos se incrustan como recursos
/// (<c>Ai/Prompts/*.txt</c>) para que vivan en el repo, se puedan diffear y se testeen una version
/// contra otra. <see cref="Latest"/> es la que usa la app; las anteriores quedan para comparar.
/// </summary>
public sealed class PromptLibrary
{
    private const string ResourcePrefix = "Foundry.Application.Ai.Prompts.";

    private readonly IReadOnlyList<PromptTemplate> _templates;

    public PromptLibrary(IEnumerable<PromptTemplate> templates)
    {
        _templates = templates
            .OrderBy(t => t.Family, StringComparer.Ordinal)
            .ThenBy(t => t.Version, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>La biblioteca por defecto, cargada de los recursos incrustados del ensamblado.</summary>
    public static PromptLibrary Default { get; } = new(LoadEmbedded());

    public IReadOnlyList<PromptTemplate> Templates => _templates;

    /// <summary>Todas las versiones de una familia, de la mas vieja a la mas nueva.</summary>
    public IReadOnlyList<PromptTemplate> Family(string family) =>
        _templates.Where(t => t.Family == family).ToList();

    /// <summary>
    /// La ultima version de una familia. El orden es lexicografico por la etiqueta de version, asi
    /// que si algun dia hay dos digitos conviene nombrarlas <c>v01</c>..<c>v10</c>.
    /// </summary>
    public PromptTemplate Latest(string family)
    {
        var versions = Family(family);
        return versions.Count > 0
            ? versions[^1]
            : throw new KeyNotFoundException($"No hay ningun prompt de la familia '{family}'.");
    }

    /// <summary>Resuelve por id completo (<c>familia@version</c>) o por familia sola (→ <see cref="Latest"/>).</summary>
    public PromptTemplate Resolve(string idOrFamily)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrFamily);

        if (idOrFamily.Contains('@', StringComparison.Ordinal))
        {
            return _templates.FirstOrDefault(t => t.Id == idOrFamily)
                   ?? throw new KeyNotFoundException($"No existe el prompt '{idOrFamily}'.");
        }

        return Latest(idOrFamily);
    }

    private static IEnumerable<PromptTemplate> LoadEmbedded()
    {
        var assembly = typeof(PromptLibrary).Assembly;

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                || !name.EndsWith(".txt", StringComparison.Ordinal))
            {
                continue;
            }

            // Formato del nombre de recurso: "<prefijo><familia>.<version>.txt"
            var relative = name[ResourcePrefix.Length..^".txt".Length];
            var dot = relative.LastIndexOf('.');
            if (dot <= 0)
            {
                continue;
            }

            var family = relative[..dot];
            var version = relative[(dot + 1)..];

            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            yield return new PromptTemplate(family, version, reader.ReadToEnd().Trim());
        }
    }
}
