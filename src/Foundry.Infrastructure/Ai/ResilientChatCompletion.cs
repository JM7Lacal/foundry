using System.Diagnostics;
using Foundry.Application.Ai;

namespace Foundry.Infrastructure.Ai;

/// <summary>
/// Envuelve a otro <see cref="IChatCompletion"/> y le agrega, sin que el resto de la app se entere:
/// <list type="bullet">
///   <item>reintentos con backoff exponencial ante fallos transitorios (<see cref="ResiliencePolicy"/>);</item>
///   <item>fallback a un segundo proveedor si el primario se agota;</item>
///   <item>una <see cref="ChatCallReport"/> por llamada (tiempo, intentos, tokens y costo estimados)
///   entregada a un <see cref="IChatCallListener"/>.</item>
/// </list>
/// Es un decorador: se registra como <c>IChatCompletion</c> en lugar del proveedor crudo.
/// </summary>
public sealed class ResilientChatCompletion : IChatCompletion
{
    private readonly IChatCompletion _primary;
    private readonly IChatCompletion? _fallback;
    private readonly ResiliencePolicy _policy;
    private readonly IChatCallListener _listener;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public ResilientChatCompletion(
        IChatCompletion primary,
        IChatCompletion? fallback = null,
        ResiliencePolicy? policy = null,
        IChatCallListener? listener = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = fallback;
        _policy = policy ?? ResiliencePolicy.Default;
        _listener = listener ?? NullChatCallListener.Instance;
        _delay = delay ?? Task.Delay;
    }

    public string Name => _primary.Name;

    public async Task<string> CompleteAsync(
        IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var startedAt = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        var promptTokens = ChatTokens.Estimate(messages);
        ChatCompletionException? lastError = null;

        for (var attempt = 1; attempt <= _policy.MaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await _delay(_policy.DelayBefore(attempt), cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var text = await _primary.CompleteAsync(messages, cancellationToken).ConfigureAwait(false);
                Emit(_primary.Name, startedAt, clock.Elapsed, attempt, ChatCallOutcome.Ok, promptTokens, text, null, null);
                return text;
            }
            catch (ChatCompletionException ex)
            {
                lastError = ex;
            }
        }

        if (_fallback is not null)
        {
            try
            {
                var text = await _fallback.CompleteAsync(messages, cancellationToken).ConfigureAwait(false);
                Emit(
                    _fallback.Name, startedAt, clock.Elapsed, _policy.MaxAttempts + 1,
                    ChatCallOutcome.FellBackOk, promptTokens, text, _fallback.Name, lastError?.Message);
                return text;
            }
            catch (ChatCompletionException ex)
            {
                lastError = ex;
            }
        }

        Emit(
            _primary.Name, startedAt, clock.Elapsed, _policy.MaxAttempts,
            ChatCallOutcome.Failed, promptTokens, string.Empty, _fallback?.Name, lastError?.Message);

        throw lastError ?? new ChatCompletionException("El proveedor no devolvio ninguna respuesta.");
    }

    private void Emit(
        string provider,
        DateTimeOffset startedAt,
        TimeSpan duration,
        int attempts,
        ChatCallOutcome outcome,
        int promptTokens,
        string completion,
        string? fallbackProvider,
        string? error)
    {
        var completionTokens = ChatTokens.Estimate(completion);
        var cost = ModelPricing.For(provider).Cost(promptTokens, completionTokens);

        var report = new ChatCallReport(
            provider,
            startedAt,
            duration,
            attempts,
            outcome,
            promptTokens,
            completionTokens,
            decimal.Round(cost, 6),
            fallbackProvider,
            error);

        try
        {
            _listener.OnCall(report);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // la observabilidad nunca debe tumbar una llamada que ya se resolvio
            Debug.WriteLine($"IChatCallListener lanzo: {ex}");
        }
    }
}
