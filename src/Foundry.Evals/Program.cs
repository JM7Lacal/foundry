using Foundry.Application.Ai;
using Foundry.Evals;
using Foundry.Infrastructure.Content;

var mode = args.Length > 0 && !args[0].StartsWith('-') ? args[0].ToLowerInvariant() : "run";
var provider = ArgValue("--provider") ?? (mode == "record" ? "claude-code" : "recorded");
var promptVersion = ArgValue("--prompt") ?? "assistant-system";
var reps = int.TryParse(ArgValue("--reps"), out var r) ? Math.Max(1, r) : (mode == "record" ? 2 : 1);
var outDir = ArgValue("--out") ?? Path.Combine(FindRepoRoot(), "artifacts");

var serializer = new JsonContentSerializer();

switch (mode)
{
    case "run":
        return await RunAsync().ConfigureAwait(false);
    case "record":
        return await RecordAsync().ConfigureAwait(false);
    default:
        Console.Error.WriteLine("Uso: dotnet run --project src/Foundry.Evals -- [run|record] [--provider p] [--prompt v] [--reps n] [--out dir]");
        return 2;
}

async Task<int> RunAsync()
{
    var runner = new EvalRunner(EvalProviders.For(provider), serializer);
    Console.WriteLine($"Corriendo {EvalCatalog.All.Count} casos · provider={provider} · prompt={promptVersion} · reps={reps}…");

    var report = await runner.RunAsync(EvalCatalog.All, promptVersion, reps).ConfigureAwait(false);

    Directory.CreateDirectory(outDir);
    var mdPath = Path.Combine(outDir, "eval-report.md");
    var jsonPath = Path.Combine(outDir, "eval-report.json");
    await File.WriteAllTextAsync(mdPath, EvalReportRenderer.ToMarkdown(report)).ConfigureAwait(false);
    await File.WriteAllTextAsync(jsonPath, EvalReportRenderer.ToJson(report)).ConfigureAwait(false);

    Console.WriteLine();
    Console.WriteLine(EvalReportRenderer.ToConsoleSummary(report));
    Console.WriteLine($"\nReporte: {mdPath}");

    return report.AllMet ? 0 : 1;
}

async Task<int> RecordAsync()
{
    if (provider is "recorded")
    {
        Console.Error.WriteLine("record necesita un proveedor real (--provider claude-code|anthropic|azure).");
        return 2;
    }

    var factory = EvalProviders.For(provider);
    foreach (var evalCase in EvalCatalog.All)
    {
        var replies = new List<string>();
        for (var i = 0; i < reps; i++)
        {
            var capturing = new CapturingChat(factory(evalCase));
            var assistant = new ContentAssistant(capturing, serializer, PromptLibrary.Default, promptVersion);
            try
            {
                await assistant.AskAsync(evalCase.Request, evalCase.Arrange(), CancellationToken.None).ConfigureAwait(false);
                if (capturing.LastReply is { } reply)
                {
                    replies.Add(reply);
                }
            }
            catch (Exception ex) when (ex is ChatCompletionException or Foundry.Application.Content.ContentRepositoryException)
            {
                Console.Error.WriteLine($"  {evalCase.Name}: {ex.Message}");
            }
        }

        if (replies.Count == 0)
        {
            Console.Error.WriteLine($"  {evalCase.Name}: sin respuestas, fixture no actualizado");
            continue;
        }

        RecordedChat.Save(evalCase.Name, replies);
        Console.WriteLine($"  {evalCase.Name}: {replies.Count} respuesta(s) grabada(s)");
    }

    Console.WriteLine($"\nFixtures en {RecordedChat.FixturesDirectory}");
    return 0;
}

string? ArgValue(string flag)
{
    var index = Array.IndexOf(args, flag);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Foundry.sln")))
    {
        dir = dir.Parent;
    }

    return dir?.FullName ?? Directory.GetCurrentDirectory();
}

/// <summary>Deja pasar la llamada y retiene el ultimo texto, para grabar fixtures.</summary>
file sealed class CapturingChat(IChatCompletion inner) : IChatCompletion
{
    public string? LastReply { get; private set; }

    public string Name => inner.Name;

    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var reply = await inner.CompleteAsync(messages, cancellationToken).ConfigureAwait(false);
        LastReply = reply;
        return reply;
    }
}
