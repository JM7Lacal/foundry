namespace Foundry.Application.Ai;

/// <summary>
/// Un system prompt versionado. El texto es fijo (vive en el repo como recurso incrustado); las
/// partes que dependen del estado — hoy solo <c>{schema}</c> — se completan en tiempo de ejecucion
/// con <see cref="Render"/>. Tener el prompt como artefacto con id permite testear una version
/// contra otra (ver <c>Foundry.Evals</c>) y saber que version produjo cada respuesta.
/// </summary>
/// <param name="Family">Nombre estable de la familia de prompts, p. ej. <c>assistant-system</c>.</param>
/// <param name="Version">Etiqueta de version dentro de la familia, p. ej. <c>v1</c>.</param>
/// <param name="Text">El texto del prompt, con marcadores <c>{clave}</c> opcionales.</param>
public sealed record PromptTemplate(string Family, string Version, string Text)
{
    /// <summary>Identificador completo: <c>familia@version</c> (p. ej. <c>assistant-system@v2</c>).</summary>
    public string Id => $"{Family}@{Version}";

    /// <summary>
    /// Devuelve el texto con cada <c>{clave}</c> reemplazado por su valor. Los marcadores que no
    /// figuran en <paramref name="values"/> se dejan como estan (no es un <c>string.Format</c>: las
    /// llaves de los ejemplos JSON del prompt no molestan).
    /// </summary>
    public string Render(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var text = Text;
        foreach (var (key, value) in values)
        {
            text = text.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        }

        return text;
    }
}
