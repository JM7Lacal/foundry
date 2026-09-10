namespace Foundry.Application.Ai;

/// <summary>
/// Politica de reintentos para las llamadas al modelo: cuantos intentos y cuanto esperar entre
/// ellos (backoff exponencial). Un modelo remoto falla de forma transitoria (429, 5xx, timeout)
/// bastante seguido; reintentar con espera creciente absorbe la mayoria sin intervencion.
/// </summary>
public sealed record ResiliencePolicy(
    int MaxAttempts = 3,
    TimeSpan? BaseDelay = null,
    double BackoffFactor = 2.0)
{
    /// <summary>Espera antes del intento numero <paramref name="attempt"/> (1 = primero, sin espera previa).</summary>
    public TimeSpan DelayBefore(int attempt)
    {
        if (attempt <= 1)
        {
            return TimeSpan.Zero;
        }

        var baseDelay = BaseDelay ?? TimeSpan.FromMilliseconds(200);
        var factor = Math.Pow(BackoffFactor, attempt - 2);
        return baseDelay * factor;
    }

    /// <summary>3 intentos, 200 ms de base, x2.</summary>
    public static ResiliencePolicy Default { get; } = new();

    /// <summary>Un solo intento: sin reintentos.</summary>
    public static ResiliencePolicy NoRetry { get; } = new(MaxAttempts: 1);
}
