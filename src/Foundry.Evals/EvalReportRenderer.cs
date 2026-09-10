using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Foundry.Evals;

/// <summary>Renderiza un <see cref="EvalReport"/> a Markdown, JSON y una linea de consola.</summary>
public static class EvalReportRenderer
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string ToMarkdown(EvalReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        sb.AppendLine("# Eval del asistente");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Fecha:** {report.RunAt:u}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Proveedor:** `{report.Provider}`");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **System prompt:** `{report.PromptId}`");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Corridas por caso:** {report.Repetitions}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- **Pass rate global:** {report.OverallPassRate:P0}  ·  **Umbral cumplido:** {(report.AllMet ? "sí" : "NO")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Costo estimado total:** US$ {report.TotalEstimatedCostUsd}");
        sb.AppendLine();

        sb.AppendLine("## Casos");
        sb.AppendLine();
        sb.AppendLine("| Caso | Categoría | Pass rate | Umbral | Latencia media | Costo medio |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|");
        foreach (var c in report.Cases)
        {
            var mark = c.MeetsThreshold ? "✅" : "❌";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {mark} {c.Case.Name} | {c.Case.Category} | {c.PassRate:P0} ({c.RunsPassed}/{c.Runs}) | {c.Case.Threshold:P0} | {c.MeanLatencyMs:0} ms | US$ {c.MeanCostUsd} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Detalle por afirmación");
        sb.AppendLine();
        foreach (var c in report.Cases)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"### {c.Case.Name}");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"> {c.Case.Request}");
            sb.AppendLine();
            foreach (var a in c.PerAssertion)
            {
                var mark = a.Passed == a.Total ? "✅" : a.Passed == 0 ? "❌" : "⚠️";
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {mark} {a.Description} — {a.Passed}/{a.Total}");
                foreach (var sample in a.FailureSamples)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  - `{sample}`");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string ToJson(EvalReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        // Proyeccion serializable: EvalCase lleva delegados (Arrange, Check) que no se serializan.
        var dto = new
        {
            report.RunAt,
            report.Provider,
            report.PromptId,
            report.Repetitions,
            OverallPassRate = report.OverallPassRate,
            report.AllMet,
            report.TotalEstimatedCostUsd,
            Cases = report.Cases.Select(c => new
            {
                c.Case.Name,
                c.Case.Category,
                c.Case.Request,
                c.Case.Threshold,
                c.PassRate,
                c.RunsPassed,
                c.Runs,
                c.MeetsThreshold,
                c.MeanLatencyMs,
                c.MeanCostUsd,
                Assertions = c.PerAssertion.Select(a => new
                {
                    a.Description,
                    a.Passed,
                    a.Total,
                    a.Rate,
                    a.FailureSamples,
                }),
            }),
        };

        return JsonSerializer.Serialize(dto, Json);
    }

    public static string ToConsoleSummary(EvalReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"eval · provider={report.Provider} · prompt={report.PromptId} · reps={report.Repetitions}");
        foreach (var c in report.Cases)
        {
            var mark = c.MeetsThreshold ? "PASS" : "FAIL";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  [{mark}] {c.Case.Name,-28} {c.PassRate,6:P0} (umbral {c.Case.Threshold:P0})");
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"global {report.OverallPassRate:P0} · {(report.AllMet ? "TODOS los umbrales cumplidos" : $"{report.Failing.Count} caso(s) por debajo del umbral")} · US$ {report.TotalEstimatedCostUsd} est.");
        return sb.ToString();
    }
}
