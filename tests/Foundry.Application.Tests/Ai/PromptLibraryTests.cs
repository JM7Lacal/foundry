using FluentAssertions;
using Foundry.Application.Ai;

namespace Foundry.Application.Tests.Ai;

public class PromptLibraryTests
{
    [Fact]
    public void Default_library_loads_the_embedded_assistant_prompts()
    {
        var family = PromptLibrary.Default.Family("assistant-system");

        family.Should().HaveCountGreaterThanOrEqualTo(2);
        family.Select(t => t.Version).Should().BeInAscendingOrder().And.Contain(["v1", "v2"]);
        family.Should().OnlyContain(t => t.Text.Contains("{schema}"));
    }

    [Fact]
    public void Latest_returns_the_highest_version_of_the_family()
    {
        var latest = PromptLibrary.Default.Latest("assistant-system");

        latest.Version.Should().Be("v2");
        latest.Id.Should().Be("assistant-system@v2");
    }

    [Fact]
    public void Resolve_accepts_a_full_id_or_a_bare_family()
    {
        PromptLibrary.Default.Resolve("assistant-system@v1").Version.Should().Be("v1");
        PromptLibrary.Default.Resolve("assistant-system").Version.Should().Be("v2");
    }

    [Fact]
    public void Resolve_throws_for_an_unknown_prompt()
    {
        var act = () => PromptLibrary.Default.Resolve("assistant-system@v99");

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Render_fills_known_placeholders_and_leaves_json_braces_alone()
    {
        var template = new PromptTemplate("t", "v1", "campos:\n{schema}\nformato: { \"answer\": \"...\" }");

        var rendered = template.Render(new Dictionary<string, string> { ["schema"] = "- troop: cost" });

        rendered.Should().Contain("- troop: cost");
        rendered.Should().Contain("{ \"answer\": \"...\" }");
        rendered.Should().NotContain("{schema}");
    }
}
