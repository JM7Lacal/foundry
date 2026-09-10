using FluentAssertions;
using Foundry.Application.Ai;
using Foundry.Core.Content;
using Foundry.Infrastructure.Content;

namespace Foundry.Infrastructure.Tests.Ai;

public class ContentAssistantTests
{
    private static ContentAssistant Assistant(string modelReply) =>
        new(new ScriptedChat(modelReply), new JsonContentSerializer());

    private static ContentDatabase Db()
    {
        var db = new ContentDatabase();
        db.Add(new Enemy { Id = new EntityId("enemy.orc"), Name = "Orco", Health = 160 });
        return db;
    }

    [Fact]
    public async Task Plain_answer_comes_back_without_a_proposal()
    {
        var result = await Assistant("""{ "answer": "La torre de cañon le gana." }""").AskAsync("¿que le gana al orco?", Db());

        result.Answer.Should().Be("La torre de cañon le gana.");
        result.HasProposal.Should().BeFalse();
    }

    [Fact]
    public async Task Proposed_entities_are_parsed_into_typed_objects()
    {
        var reply =
            """
            { "rationale": "Un lancero anti-orco",
              "entities": [ { "$type": "troop", "id": "troop.orc-slayer", "name": "Cazador de orcos",
                             "cost": 150, "damage": 34, "damageType": "physical", "health": 120, "armor": "light" } ] }
            """;

        var result = await Assistant(reply).AskAsync("crea un counter del orco", Db());

        result.Rationale.Should().Contain("anti-orco");
        result.ProposedEntities.Should().ContainSingle();
        var troop = result.ProposedEntities.OfType<Troop>().Single();
        troop.Id.Should().Be(new EntityId("troop.orc-slayer"));
        troop.Damage.Should().Be(34);
        troop.DamageType.Should().Be(DamageType.Physical);
    }

    [Fact]
    public async Task Strips_a_markdown_code_fence_around_the_json()
    {
        var reply = "Claro:\n```json\n{ \"answer\": \"listo\" }\n```\n";

        (await Assistant(reply).AskAsync("hola", Db())).Answer.Should().Be("listo");
    }

    [Fact]
    public async Task A_reply_that_ignores_the_json_format_is_returned_as_a_plain_answer()
    {
        var result = await Assistant("Perdon, no entiendo el pedido.").AskAsync("hola", Db());

        result.Answer.Should().Be("Perdon, no entiendo el pedido.");
        result.HasProposal.Should().BeFalse();
    }

    [Fact]
    public async Task Malformed_entity_json_raises_an_error()
    {
        var act = () => Assistant("""{ "entities": [ { "$type": "nave-espacial", "id": "x" } ] }""")
            .AskAsync("crea algo", Db());

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task The_result_carries_the_prompt_version_that_produced_it()
    {
        var pinned = new ContentAssistant(
            new ScriptedChat("""{ "answer": "ok" }"""),
            new JsonContentSerializer(),
            Foundry.Application.Ai.PromptLibrary.Default,
            "assistant-system@v1");

        pinned.PromptId.Should().Be("assistant-system@v1");
        (await pinned.AskAsync("hola", Db())).PromptId.Should().Be("assistant-system@v1");
    }

    private sealed class ScriptedChat : IChatCompletion
    {
        private readonly string _reply;

        public ScriptedChat(string reply) => _reply = reply;

        public string Name => "scripted";

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(_reply);
    }
}
