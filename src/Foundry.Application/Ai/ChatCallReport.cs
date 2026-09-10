namespace Foundry.Application.Ai;

/// <summary>Como termino una llamada al modelo.</summary>
public enum ChatCallOutcome
{
    /// <summary>El proveedor primario respondio (con o sin reintentos).</summary>
    Ok,

    /// <summary>El primario fallo y respondio el de respaldo.</summary>
    FellBackOk,

    /// <summary>Fallaron el primario y el de respaldo (si habia).</summary>
    Failed,
}

/// <summary>
/// Traza de una llamada a un <see cref="IChatCompletion"/>: proveedor, tiempo, reintentos, tokens
/// y costo estimados, resultado. La emite <c>ResilientChatCompletion</c> en cada llamada y la
/// consume un <see cref="IChatCallListener"/> (log, panel, metricas). Tokens y costo son
/// ESTIMACIONES basadas en el largo del texto y precios de lista, no la factura real.
/// </summary>
public sealed record ChatCallReport(
    string Provider,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    int Attempts,
    ChatCallOutcome Outcome,
    int PromptTokensEstimate,
    int CompletionTokensEstimate,
    decimal EstimatedCostUsd,
    string? FallbackProvider = null,
    string? Error = null)
{
    public int TotalTokensEstimate => PromptTokensEstimate + CompletionTokensEstimate;

    public bool Succeeded => Outcome is ChatCallOutcome.Ok or ChatCallOutcome.FellBackOk;
}
