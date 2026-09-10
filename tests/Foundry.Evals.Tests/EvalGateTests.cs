using FluentAssertions;
using Foundry.Evals;
using Foundry.Infrastructure.Content;

namespace Foundry.Evals.Tests;

/// <summary>
/// El gate de CI: cada caso del catalogo, corrido varias veces contra su fixture grabado, tiene
/// que alcanzar su umbral de pass rate. Como los fixtures son deterministas, esto detecta
/// regresiones en el parser, el esquema, el prompt o las afirmaciones — no la calidad del modelo
/// (para eso esta la corrida live opcional).
/// </summary>
public class EvalGateTests
{
    private const int Repetitions = 5;

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var evalCase in EvalCatalog.All)
        {
            data.Add(evalCase.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task Case_meets_its_threshold_against_the_recorded_fixture(string caseName)
    {
        var evalCase = EvalCatalog.ByName(caseName);
        var runner = new EvalRunner(EvalProviders.For("recorded"), new JsonContentSerializer());

        var report = await runner.RunAsync([evalCase], "assistant-system", Repetitions);

        var outcome = report.Cases.Single();
        outcome.MeetsThreshold.Should().BeTrue(
            "pass rate {0:P0} < umbral {1:P0}\n{2}",
            outcome.PassRate,
            evalCase.Threshold,
            FormatFailures(outcome));
    }

    [Fact]
    public async Task The_full_run_produces_a_markdown_report()
    {
        var runner = new EvalRunner(EvalProviders.For("recorded"), new JsonContentSerializer());

        var report = await runner.RunAsync(EvalCatalog.All, "assistant-system", 2);
        var markdown = EvalReportRenderer.ToMarkdown(report);

        report.Cases.Should().HaveCount(EvalCatalog.All.Count);
        markdown.Should().Contain("# Eval del asistente").And.Contain("Pass rate global");
        EvalReportRenderer.ToJson(report).Should().Contain("\"AllMet\"");
    }

    [Fact]
    public async Task Recorded_fixtures_all_exercise_a_real_assistant_path()
    {
        // Ningun caso deberia estar "verde" porque la llamada fallo: exigimos al menos
        // una afirmacion evaluada y una traza de llamada por caso.
        var runner = new EvalRunner(EvalProviders.For("recorded"), new JsonContentSerializer());
        var report = await runner.RunAsync(EvalCatalog.All, "assistant-system", 1);

        report.Cases.Should().OnlyContain(c => c.PerAssertion.Count > 0);
        report.OverallPassRate.Should().BeGreaterThan(0.9);
    }

    private static string FormatFailures(CaseOutcome outcome) =>
        string.Join(
            "\n",
            outcome.PerAssertion
                .Where(a => a.Passed < a.Total)
                .Select(a => $"  - {a.Description}: {a.Passed}/{a.Total} · {string.Join(" | ", a.FailureSamples)}"));
}
