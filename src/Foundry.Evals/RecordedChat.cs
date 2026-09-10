using System.Text.Json;
using Foundry.Application.Ai;

namespace Foundry.Evals;

/// <summary>
/// Reproduce respuestas grabadas de un modelo real, una por caso, leidas de
/// <c>Fixtures/&lt;caso&gt;.json</c> (<c>{ "replies": ["...", "..."] }</c>). Si un caso pide mas
/// corridas que respuestas grabadas, cicla. Es lo que usa el gate de CI: determinista y sin red.
/// </summary>
public sealed class RecordedChat : IChatCompletion
{
    private static readonly JsonSerializerOptions WriteOptions =
        new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly JsonSerializerOptions ReadOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly IReadOnlyList<string> _replies;
    private int _index;

    private RecordedChat(IReadOnlyList<string> replies) => _replies = replies;

    public string Name => "recorded";

    public static string FixturesDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static RecordedChat ForCase(string caseName)
    {
        var path = Path.Combine(FixturesDirectory, $"{caseName}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Falta el fixture del caso '{caseName}'. Generalo con: dotnet run --project src/Foundry.Evals -- record",
                path);
        }

        var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(path), ReadOptions)
                      ?? throw new InvalidDataException($"Fixture vacio o invalido: {path}");

        if (fixture.Replies is not { Count: > 0 })
        {
            throw new InvalidDataException($"El fixture '{caseName}' no tiene respuestas.");
        }

        return new RecordedChat(fixture.Replies);
    }

    public static void Save(string caseName, IEnumerable<string> replies)
    {
        Directory.CreateDirectory(FixturesDirectory);
        var path = Path.Combine(FixturesDirectory, $"{caseName}.json");
        var fixture = new Fixture { Replies = replies.ToList() };
        File.WriteAllText(path, JsonSerializer.Serialize(fixture, WriteOptions));
    }

    public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var reply = _replies[_index % _replies.Count];
        _index++;
        return Task.FromResult(reply);
    }

    private sealed class Fixture
    {
        public List<string> Replies { get; set; } = [];
    }
}
