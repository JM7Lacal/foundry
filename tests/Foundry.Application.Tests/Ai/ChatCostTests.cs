using FluentAssertions;
using Foundry.Application.Ai;

namespace Foundry.Application.Tests.Ai;

public class ChatCostTests
{
    [Fact]
    public void Token_estimate_grows_with_text_length_and_is_zero_for_empty()
    {
        ChatTokens.Estimate(string.Empty).Should().Be(0);
        ChatTokens.Estimate("cuatro123").Should().BeInRange(2, 3);
        ChatTokens.Estimate(new string('x', 400)).Should().BeInRange(95, 105);
    }

    [Fact]
    public void Conversation_estimate_adds_per_message_overhead()
    {
        var messages = new ChatMessage[] { new("system", "abcd"), new("user", "abcd") };

        // 1 token de texto + 4 de overhead, por cada mensaje
        ChatTokens.Estimate(messages).Should().Be(10);
    }

    [Fact]
    public void Pricing_matches_by_provider_name_prefix()
    {
        ModelPricing.For("azure:gpt-4o-mini").InputPer1K.Should().BeGreaterThan(0);
        ModelPricing.For("anthropic:claude-haiku-4-5-20251001").OutputPer1K.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Unknown_or_local_providers_cost_nothing()
    {
        ModelPricing.For("ollama:qwen2.5").Should().Be(ModelRate.Free);
        ModelPricing.For("stub").Should().Be(ModelRate.Free);
        ModelPricing.For("claude-code").Should().Be(ModelRate.Free);
        ModelPricing.For("algo-desconocido").Should().Be(ModelRate.Free);
    }

    [Fact]
    public void Cost_is_input_plus_output_priced_per_thousand_tokens()
    {
        var rate = new ModelRate(InputPer1K: 0.001m, OutputPer1K: 0.002m);

        rate.Cost(2000, 1000).Should().Be((0.002m) + (0.002m));
    }
}
