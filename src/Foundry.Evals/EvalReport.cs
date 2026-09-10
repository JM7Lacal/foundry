using Foundry.Application.Ai;

namespace Foundry.Evals;

/// <summary>Una afirmacion evaluada en una corrida.</summary>
public sealed record AssertionResult(string Description, EvalCheck Check);

/// <summary>Una corrida de un caso: sus afirmaciones y la traza de la llamada.</summary>
public sealed record CaseRun(IReadOnlyList<AssertionResult> Assertions, ChatCallReport? Call)
{
    /// <summary>La corrida pasa si TODAS sus afirmaciones pasan.</summary>
    public bool Passed => Assertions.All(a => a.Check.Passed);
}

/// <summary>Pass rate de una afirmacion sobre todas las corridas de un caso.</summary>
public sealed record AssertionStat(string Description, int Passed, int Total, IReadOnlyList<string> FailureSamples)
{
    public double Rate => Total == 0 ? 0 : (double)Passed / Total;
}

/// <summary>Resultado agregado de un caso tras N corridas.</summary>
public sealed record CaseOutcome(
    EvalCase Case,
    int Runs,
    int RunsPassed,
    IReadOnlyList<AssertionStat> PerAssertion,
    double MeanLatencyMs,
    decimal MeanCostUsd)
{
    public double PassRate => Runs == 0 ? 0 : (double)RunsPassed / Runs;

    public bool MeetsThreshold => PassRate >= Case.Threshold;

    public static CaseOutcome Aggregate(EvalCase evalCase, IReadOnlyList<CaseRun> runs)
    {
        var perAssertion = evalCase.Assertions.Select((assertion, index) =>
        {
            var checks = runs.Select(r => r.Assertions[index].Check).ToList();
            var failures = checks
                .Where(c => !c.Passed && !string.IsNullOrWhiteSpace(c.Detail))
                .Select(c => c.Detail!)
                .Distinct()
                .Take(3)
                .ToList();
            return new AssertionStat(assertion.Description, checks.Count(c => c.Passed), checks.Count, failures);
        }).ToList();

        var calls = runs.Where(r => r.Call is not null).Select(r => r.Call!).ToList();
        var meanLatency = calls.Count == 0 ? 0 : calls.Average(c => c.Duration.TotalMilliseconds);
        var meanCost = calls.Count == 0 ? 0m : calls.Sum(c => c.EstimatedCostUsd) / calls.Count;

        return new CaseOutcome(
            evalCase, runs.Count, runs.Count(r => r.Passed), perAssertion, meanLatency, decimal.Round(meanCost, 6));
    }
}

/// <summary>El reporte completo de una ejecucion del harness.</summary>
public sealed record EvalReport(
    DateTimeOffset RunAt,
    string Provider,
    string PromptId,
    int Repetitions,
    IReadOnlyList<CaseOutcome> Cases)
{
    public double OverallPassRate =>
        Cases.Count == 0 ? 0 : Cases.Average(c => c.PassRate);

    public bool AllMet => Cases.All(c => c.MeetsThreshold);

    public IReadOnlyList<CaseOutcome> Failing => Cases.Where(c => !c.MeetsThreshold).ToList();

    public decimal TotalEstimatedCostUsd =>
        decimal.Round(Cases.Sum(c => c.MeanCostUsd * c.Runs), 6);
}
