using Foundry.Application.Ai;
using Foundry.Application.Content;
using Foundry.Application.Editing;
using Foundry.Application.Validation;
using Foundry.Core.Content;
using Foundry.Infrastructure.Ai;

namespace Foundry.Evals;

/// <summary>
/// Corre cada <see cref="EvalCase"/> <c>repetitions</c> veces contra un proveedor y agrega los
/// resultados. Cada corrida arma la misma pila que la app (proveedor → decorator → asistente),
/// capturando la respuesta cruda y la <see cref="ChatCallReport"/>.
/// </summary>
public sealed class EvalRunner
{
    private readonly Func<EvalCase, IChatCompletion> _provider;
    private readonly IContentSerializer _serializer;
    private readonly ContentValidator _validator = new();
    private readonly PromptLibrary _prompts;

    /// <param name="provider">
    /// Da el proveedor a usar para un caso. Los proveedores reales ignoran el caso; el grabado
    /// (<see cref="RecordedChat"/>) carga el fixture de ese caso.
    /// </param>
    public EvalRunner(
        Func<EvalCase, IChatCompletion> provider, IContentSerializer serializer, PromptLibrary? prompts = null)
    {
        _provider = provider;
        _serializer = serializer;
        _prompts = prompts ?? PromptLibrary.Default;
    }

    public async Task<EvalReport> RunAsync(
        IReadOnlyList<EvalCase> cases,
        string promptVersion,
        int repetitions,
        CancellationToken cancellationToken = default)
    {
        var outcomes = new List<CaseOutcome>();
        var providerName = "?";

        foreach (var evalCase in cases)
        {
            var runs = new List<CaseRun>();
            for (var i = 0; i < repetitions; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var run = await RunOnceAsync(evalCase, promptVersion, cancellationToken).ConfigureAwait(false);
                runs.Add(run);
                providerName = run.Call?.Provider ?? providerName;
            }

            outcomes.Add(CaseOutcome.Aggregate(evalCase, runs));
        }

        return new EvalReport(
            DateTimeOffset.UtcNow, providerName, _prompts.Resolve(promptVersion).Id, repetitions, outcomes);
    }

    private async Task<CaseRun> RunOnceAsync(EvalCase evalCase, string promptVersion, CancellationToken cancellationToken)
    {
        var database = evalCase.Arrange();
        var capturing = new CapturingChat(_provider(evalCase));
        var listener = new LastCallListener();
        var chat = new ResilientChatCompletion(capturing, policy: ResiliencePolicy.NoRetry, listener: listener);
        var assistant = new ContentAssistant(chat, _serializer, _prompts, promptVersion);

        AssistantResult result;
        string? error = null;
        try
        {
            result = await assistant.AskAsync(evalCase.Request, database, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ChatCompletionException or ContentRepositoryException)
        {
            error = ex.Message;
            result = new AssistantResult(null, null, Array.Empty<ContentEntity>(), promptVersion);
        }

        var context = new EvalContext(
            evalCase.Request, database, capturing.LastReply ?? string.Empty, result, listener.Last, _validator, error);

        var assertions = evalCase.Assertions
            .Select(a => new AssertionResult(a.Description, Evaluate(a, context, error)))
            .ToList();

        return new CaseRun(assertions, listener.Last);
    }

    private static EvalCheck Evaluate(EvalAssertion assertion, EvalContext context, string? error)
    {
        if (error is not null)
        {
            return EvalCheck.Fail($"la llamada fallo: {error}");
        }

        try
        {
            return assertion.Check(context);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return EvalCheck.Fail($"la afirmacion lanzo: {ex.Message}");
        }
    }

    /// <summary>Deja pasar la llamada al proveedor real y se queda con el ultimo texto devuelto.</summary>
    private sealed class CapturingChat : IChatCompletion
    {
        private readonly IChatCompletion _inner;

        public CapturingChat(IChatCompletion inner) => _inner = inner;

        public string? LastReply { get; private set; }

        public string Name => _inner.Name;

        public async Task<string> CompleteAsync(
            IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            var reply = await _inner.CompleteAsync(messages, cancellationToken).ConfigureAwait(false);
            LastReply = reply;
            return reply;
        }
    }

    private sealed class LastCallListener : IChatCallListener
    {
        public ChatCallReport? Last { get; private set; }

        public void OnCall(ChatCallReport report) => Last = report;
    }
}
