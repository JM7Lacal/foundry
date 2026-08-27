using FluentAssertions;
using Foundry.Application.Ai;
using Foundry.Core.Content;
using Foundry.Presentation.ViewModels;

namespace Foundry.Presentation.Tests.ViewModels;

public class AssistantViewModelTests
{
    private static ContentDatabase Db()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("troop.knight"), Name = "Caballero", Damage = 28 });
        return db;
    }

    private static AssistantViewModel Build(string reply, out FakeSerializer serializer)
    {
        serializer = new FakeSerializer();
        return new AssistantViewModel(new ContentAssistant(new ScriptedChat(reply), serializer));
    }

    [Fact]
    public void Ask_is_disabled_until_there_is_a_prompt_and_a_context()
    {
        var vm = Build("""{ "answer": "ok" }""", out _);

        vm.AskCommand.CanExecute(null).Should().BeFalse();

        vm.Prompt = "hola";
        vm.AskCommand.CanExecute(null).Should().BeFalse(); // todavia sin contexto

        vm.SetContext(Db());
        vm.AskCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task A_plain_answer_is_shown_and_leaves_no_proposal()
    {
        var vm = Build("""{ "answer": "La torre de cañon le gana." }""", out _);
        vm.SetContext(Db());
        vm.Prompt = "¿que le gana al orco?";

        await vm.AskCommand.ExecuteAsync(null);

        vm.Answer.Should().Be("La torre de cañon le gana.");
        vm.HasProposal.Should().BeFalse();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task A_proposal_can_be_previewed_and_applied()
    {
        var reply =
            """{ "rationale": "un mago", "entities": [ { "$type": "troop", "id": "troop.mage", "name": "Mago" } ] }""";
        var vm = Build(reply, out _);
        vm.SetContext(Db());
        vm.Prompt = "crea un mago";
        var applied = 0;
        vm.ProposalApplied += (_, _) => applied++;

        await vm.AskCommand.ExecuteAsync(null);

        vm.HasProposal.Should().BeTrue();
        vm.ProposalPreview.Should().ContainSingle();
        vm.ApplyProposalCommand.CanExecute(null).Should().BeTrue();

        vm.ApplyProposalCommand.Execute(null);

        applied.Should().Be(1);
        vm.HasProposal.Should().BeFalse();
    }

    [Fact]
    public void Quick_actions_need_a_selected_entity()
    {
        var vm = Build("""{ "answer": "ok" }""", out _);
        vm.SetContext(Db());

        vm.AnalyzeEntityCommand.CanExecute(null).Should().BeFalse();

        vm.SetSelectedEntity(new Troop { Id = new EntityId("troop.knight"), Name = "Caballero" });
        vm.AnalyzeEntityCommand.CanExecute(null).Should().BeTrue();
        vm.ExplainUpgradesCommand.CanExecute(null).Should().BeTrue();
        vm.CheckBalanceCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task A_quick_action_fills_the_prompt_and_runs_it()
    {
        var vm = Build("""{ "answer": "El caballero esta bien." }""", out _);
        vm.SetContext(Db());
        vm.SetSelectedEntity(new Troop { Id = new EntityId("troop.knight"), Name = "Caballero" });

        await vm.AnalyzeEntityCommand.ExecuteAsync(null);

        vm.Prompt.Should().Contain("Caballero");
        vm.Answer.Should().Be("El caballero esta bien.");
    }

    [Fact]
    public async Task A_provider_failure_is_surfaced_as_an_error()
    {
        var vm = new AssistantViewModel(new ContentAssistant(new FailingChat(), new FakeSerializer()));
        vm.SetContext(Db());
        vm.Prompt = "hola";

        await vm.AskCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.IsBusy.Should().BeFalse();
    }

    private sealed class ScriptedChat : IChatCompletion
    {
        private readonly string _reply;

        public ScriptedChat(string reply) => _reply = reply;

        public string Name => "scripted";

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(_reply);
    }

    private sealed class FailingChat : IChatCompletion
    {
        public string Name => "failing";

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
            => throw new ChatCompletionException("proveedor caido");
    }

    private sealed class FakeSerializer : Foundry.Application.Editing.IContentSerializer
    {
        public string SerializeEntity(ContentEntity entity) => "{}";

        public IReadOnlyList<ContentEntity> DeserializeEntities(string json) =>
            json.Contains("troop.mage", StringComparison.Ordinal)
                ? [new Troop { Id = new EntityId("troop.mage"), Name = "Mago" }]
                : Array.Empty<ContentEntity>();
    }
}
