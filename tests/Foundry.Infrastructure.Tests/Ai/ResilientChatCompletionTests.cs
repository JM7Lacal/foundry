using FluentAssertions;
using Foundry.Application.Ai;
using Foundry.Infrastructure.Ai;

namespace Foundry.Infrastructure.Tests.Ai;

public class ResilientChatCompletionTests
{
    private static readonly ChatMessage[] Prompt =
    [
        new("system", "sos un asistente"),
        new("user", "hola que tal como estas"),
    ];

    private readonly CollectingListener _listener = new();

    // delay que no espera de verdad: cuenta las esperas y devuelve al toque
    private readonly List<TimeSpan> _waits = [];

    private Func<TimeSpan, CancellationToken, Task> NoWait =>
        (delay, _) =>
        {
            _waits.Add(delay);
            return Task.CompletedTask;
        };

    [Fact]
    public async Task Passes_through_on_the_first_attempt_and_reports_it()
    {
        var sut = new ResilientChatCompletion(
            new FakeChat("stub", _ => "ok"), listener: _listener, delay: NoWait);

        var answer = await sut.CompleteAsync(Prompt);

        answer.Should().Be("ok");
        _waits.Should().BeEmpty();
        var report = _listener.Single;
        report.Outcome.Should().Be(ChatCallOutcome.Ok);
        report.Attempts.Should().Be(1);
        report.PromptTokensEstimate.Should().BeGreaterThan(0);
        report.CompletionTokensEstimate.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Retries_transient_failures_then_succeeds()
    {
        var attempts = 0;
        var sut = new ResilientChatCompletion(
            new FakeChat("anthropic:claude-haiku", _ =>
                ++attempts < 3 ? throw new ChatCompletionException("429") : "listo"),
            policy: new ResiliencePolicy(MaxAttempts: 3, BaseDelay: TimeSpan.FromMilliseconds(10)),
            listener: _listener,
            delay: NoWait);

        var answer = await sut.CompleteAsync(Prompt);

        answer.Should().Be("listo");
        attempts.Should().Be(3);
        _waits.Should().HaveCount(2); // esperas antes del 2do y 3er intento
        _listener.Single.Outcome.Should().Be(ChatCallOutcome.Ok);
        _listener.Single.Attempts.Should().Be(3);
    }

    [Fact]
    public async Task Falls_back_to_the_secondary_when_the_primary_is_exhausted()
    {
        var sut = new ResilientChatCompletion(
            primary: new FakeChat("anthropic:x", _ => throw new ChatCompletionException("caido")),
            fallback: new FakeChat("stub", _ => "respuesta del respaldo"),
            policy: new ResiliencePolicy(MaxAttempts: 2),
            listener: _listener,
            delay: NoWait);

        var answer = await sut.CompleteAsync(Prompt);

        answer.Should().Be("respuesta del respaldo");
        var report = _listener.Single;
        report.Outcome.Should().Be(ChatCallOutcome.FellBackOk);
        report.Provider.Should().Be("stub");
        report.FallbackProvider.Should().Be("stub");
        report.Error.Should().Contain("caido");
    }

    [Fact]
    public async Task Throws_and_reports_failure_when_primary_and_fallback_both_fail()
    {
        var sut = new ResilientChatCompletion(
            primary: new FakeChat("azure:gpt-4o-mini", _ => throw new ChatCompletionException("500")),
            fallback: new FakeChat("stub", _ => throw new ChatCompletionException("tambien caido")),
            policy: new ResiliencePolicy(MaxAttempts: 2),
            listener: _listener,
            delay: NoWait);

        var act = () => sut.CompleteAsync(Prompt);

        await act.Should().ThrowAsync<ChatCompletionException>();
        _listener.Single.Outcome.Should().Be(ChatCallOutcome.Failed);
    }

    [Fact]
    public async Task Estimates_cost_from_the_provider_name()
    {
        var priced = new ResilientChatCompletion(
            new FakeChat("azure:gpt-4o-mini", _ => "una respuesta cualquiera"), listener: _listener, delay: NoWait);
        await priced.CompleteAsync(Prompt);
        _listener.Single.EstimatedCostUsd.Should().BeGreaterThan(0);

        _listener.Clear();

        var free = new ResilientChatCompletion(
            new FakeChat("stub", _ => "una respuesta cualquiera"), listener: _listener, delay: NoWait);
        await free.CompleteAsync(Prompt);
        _listener.Single.EstimatedCostUsd.Should().Be(0);
    }

    [Fact]
    public async Task A_throwing_listener_does_not_break_a_resolved_call()
    {
        var sut = new ResilientChatCompletion(
            new FakeChat("stub", _ => "ok"),
            listener: new ThrowingListener(),
            delay: NoWait);

        (await sut.CompleteAsync(Prompt)).Should().Be("ok");
    }

    private sealed class FakeChat : IChatCompletion
    {
        private readonly Func<IReadOnlyList<ChatMessage>, string> _reply;

        public FakeChat(string name, Func<IReadOnlyList<ChatMessage>, string> reply)
        {
            Name = name;
            _reply = reply;
        }

        public string Name { get; }

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(_reply(messages));
    }

    private sealed class CollectingListener : IChatCallListener
    {
        private readonly List<ChatCallReport> _reports = [];

        public ChatCallReport Single => _reports.Should().ContainSingle().Subject;

        public void OnCall(ChatCallReport report) => _reports.Add(report);

        public void Clear() => _reports.Clear();
    }

    private sealed class ThrowingListener : IChatCallListener
    {
        public void OnCall(ChatCallReport report) => throw new InvalidOperationException("boom");
    }
}
