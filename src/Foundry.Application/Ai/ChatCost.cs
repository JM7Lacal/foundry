namespace Foundry.Application.Ai;

/// <summary>
/// Estimacion barata de tokens a partir del largo del texto. Heuristica de ~4 caracteres por
/// token (mezcla es/en); sirve para dimensionar costo y detectar prompts que se van de escala,
/// no para facturar. Un proveedor que devuelva el conteo real deberia preferirse a esto.
/// </summary>
public static class ChatTokens
{
    public static int Estimate(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    /// <summary>Estimacion para una conversacion completa (con un pequeño overhead por mensaje).</summary>
    public static int Estimate(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return messages.Sum(m => Estimate(m.Content) + 4);
    }
}

/// <summary>Precio de lista de un modelo, en USD por cada 1000 tokens.</summary>
public sealed record ModelRate(decimal InputPer1K, decimal OutputPer1K)
{
    public decimal Cost(int promptTokens, int completionTokens) =>
        (promptTokens / 1000m * InputPer1K) + (completionTokens / 1000m * OutputPer1K);

    /// <summary>Modelo sin precio conocido: no se estima costo.</summary>
    public static ModelRate Free { get; } = new(0m, 0m);
}

/// <summary>
/// Tabla de precios de lista aproximados, indexada por prefijo del <see cref="IChatCompletion.Name"/>
/// (p. ej. <c>azure:gpt-4o-mini</c>). Es una estimacion para el panel de costos; la factura real
/// la da el proveedor. Actualizar a mano cuando cambien los precios.
/// </summary>
public static class ModelPricing
{
    private static readonly (string Prefix, ModelRate Rate)[] Table =
    [
        ("azure:gpt-4o-mini", new ModelRate(0.000165m, 0.00066m)),
        ("azure:gpt-4o", new ModelRate(0.00275m, 0.011m)),
        ("openai:gpt-4o-mini", new ModelRate(0.00015m, 0.0006m)),
        ("openai:gpt-4o", new ModelRate(0.0025m, 0.01m)),
        ("anthropic:claude-haiku", new ModelRate(0.0008m, 0.004m)),
        ("anthropic:claude-3-5-haiku", new ModelRate(0.0008m, 0.004m)),
        ("anthropic:claude-sonnet", new ModelRate(0.003m, 0.015m)),
        ("anthropic:claude-haiku-4", new ModelRate(0.001m, 0.005m)),
        ("ollama", ModelRate.Free),
        ("stub", ModelRate.Free),
        ("claude-code", ModelRate.Free),
    ];

    public static ModelRate For(string providerName)
    {
        ArgumentNullException.ThrowIfNull(providerName);

        foreach (var (prefix, rate) in Table)
        {
            if (providerName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return rate;
            }
        }

        return ModelRate.Free;
    }
}
