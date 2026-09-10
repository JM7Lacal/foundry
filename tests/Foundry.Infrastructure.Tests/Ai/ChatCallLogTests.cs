using System.Text.Json;
using FluentAssertions;
using Foundry.Application.Ai;
using Foundry.Infrastructure.Ai;

namespace Foundry.Infrastructure.Tests.Ai;

public class ChatCallLogTests
{
    private static ChatCallReport Report(string provider) => new(
        provider,
        DateTimeOffset.UtcNow,
        TimeSpan.FromMilliseconds(120),
        Attempts: 1,
        ChatCallOutcome.Ok,
        PromptTokensEstimate: 300,
        CompletionTokensEstimate: 80,
        EstimatedCostUsd: 0.0001m);

    [Fact]
    public void Keeps_only_the_last_N_calls_in_memory()
    {
        var log = new ChatCallLog(capacity: 3);

        foreach (var name in new[] { "a", "b", "c", "d", "e" })
        {
            log.OnCall(Report(name));
        }

        log.Recent.Select(r => r.Provider).Should().Equal("c", "d", "e");
    }

    [Fact]
    public void Appends_one_json_line_per_call_when_given_a_path()
    {
        var path = Path.Combine(Path.GetTempPath(), $"foundry-calls-{Guid.NewGuid():N}.jsonl");
        try
        {
            var log = new ChatCallLog(filePath: path);
            log.OnCall(Report("azure:gpt-4o-mini"));
            log.OnCall(Report("stub"));

            var lines = File.ReadAllLines(path);
            lines.Should().HaveCount(2);
            JsonDocument.Parse(lines[0]).RootElement.GetProperty("Provider").GetString()
                .Should().Be("azure:gpt-4o-mini");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_bad_path_does_not_throw()
    {
        var log = new ChatCallLog(filePath: "Z:\\no-existe\\ni-nunca\\calls.jsonl");

        var act = () => log.OnCall(Report("stub"));

        act.Should().NotThrow();
        log.Recent.Should().ContainSingle();
    }
}
