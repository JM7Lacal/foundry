using System.Globalization;
using System.Text.RegularExpressions;
using Foundry.Application.Validation;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Evals;

/// <summary>
/// Afirmaciones reutilizables sobre el resultado del asistente. Son deterministas: dado un
/// resultado, pasan o no — la no-determinacion vive en el modelo, no en la verificacion.
/// </summary>
public static partial class Expect
{
    private static readonly Regex IdPattern = BuildIdPattern();

    public static EvalAssertion ProposesEntities(int min = 1, int max = int.MaxValue) => new(
        max == int.MaxValue ? $"propone al menos {min} entidad(es)" : $"propone entre {min} y {max} entidades",
        ctx =>
        {
            var n = ctx.Result.ProposedEntities.Count;
            return EvalCheck.From(n >= min && n <= max, $"propuso {n}");
        });

    public static EvalAssertion AnswersWithoutProposal() => new(
        "responde con texto y sin proponer entidades",
        ctx => EvalCheck.From(
            !ctx.Result.HasProposal && !string.IsNullOrWhiteSpace(ctx.Result.Answer),
            ctx.Result.HasProposal
                ? $"propuso {ctx.Result.ProposedEntities.Count} entidad(es)"
                : "no devolvio texto en answer"));

    public static EvalAssertion ProposedAreOfType<T>() where T : ContentEntity => new(
        $"todas las entidades propuestas son {typeof(T).Name}",
        ctx =>
        {
            var offenders = ctx.Result.ProposedEntities.Where(e => e is not T).Select(e => e.GetType().Name).ToList();
            return EvalCheck.From(ctx.Result.HasProposal && offenders.Count == 0,
                offenders.Count > 0 ? $"tambien propuso: {string.Join(", ", offenders)}" : "no hubo propuesta");
        });

    public static EvalAssertion AllProposedEntitiesValid() => new(
        "las entidades propuestas pasan la validacion (sin errores)",
        ctx =>
        {
            if (!ctx.Result.HasProposal)
            {
                return EvalCheck.Fail("no hubo propuesta");
            }

            var merged = Merge(ctx.Database, ctx.Result.ProposedEntities);
            var proposedIds = ctx.Result.ProposedEntities.Select(e => e.Id).ToHashSet();
            var errors = ctx.Validator.Validate(merged)
                .Where(i => i.Severity == ValidationSeverity.Error && proposedIds.Contains(i.EntityId))
                .ToList();

            return EvalCheck.From(errors.Count == 0, string.Join(" | ", errors.Select(e => e.ToString())));
        });

    public static EvalAssertion NoNewValidationWarnings() => new(
        "no introduce advertencias de balance nuevas",
        ctx =>
        {
            var before = ctx.Validator.Validate(ctx.Database).Count(i => i.Severity == ValidationSeverity.Warning);
            var after = ctx.Validator.Validate(Merge(ctx.Database, ctx.Result.ProposedEntities))
                .Count(i => i.Severity == ValidationSeverity.Warning);
            return EvalCheck.From(after <= before, $"advertencias: {before} -> {after}");
        });

    public static EvalAssertion IdsFollowConvention() => new(
        "los id propuestos son <tipo>.<kebab-case>",
        ctx =>
        {
            if (!ctx.Result.HasProposal)
            {
                return EvalCheck.Fail("no hubo propuesta");
            }

            var bad = ctx.Result.ProposedEntities
                .Select(e => e.Id.Value)
                .Where(id => !IdPattern.IsMatch(id))
                .ToList();
            return EvalCheck.From(bad.Count == 0, $"id fuera de convencion: {string.Join(", ", bad)}");
        });

    public static EvalAssertion KeepsExistingId(string existingId) => new(
        $"al modificar, reutiliza el id {existingId}",
        ctx => EvalCheck.From(
            ctx.Result.ProposedEntities.Any(e => e.Id.Value == existingId),
            $"propuso: {string.Join(", ", ctx.Result.ProposedEntities.Select(e => e.Id.Value))}"));

    public static EvalAssertion ProposedFieldInRange(string idContains, string field, double min, double max) => new(
        $"{field} {Target(idContains)} queda en [{min}, {max}]",
        ctx =>
        {
            var entity = string.IsNullOrEmpty(idContains)
                ? ctx.Result.ProposedEntities.Count > 0 ? ctx.Result.ProposedEntities[0] : null
                : ctx.Result.ProposedEntities
                    .FirstOrDefault(e => e.Id.Value.Contains(idContains, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                return EvalCheck.Fail($"ninguna entidad propuesta {Target(idContains)}");
            }

            if (!TryReadNumber(entity, field, out var value))
            {
                return EvalCheck.Fail($"no se pudo leer «{field}»");
            }

            return EvalCheck.From(value >= min && value <= max, $"{field} = {value}");
        });

    public static EvalAssertion ProposedFieldLessThan(string idContains, string field, double ceiling) => new(
        $"{field} de «{idContains}» es menor que {ceiling}",
        ctx =>
        {
            var entity = ctx.Result.ProposedEntities
                .FirstOrDefault(e => e.Id.Value.Contains(idContains, StringComparison.OrdinalIgnoreCase));
            if (entity is null || !TryReadNumber(entity, field, out var value))
            {
                return EvalCheck.Fail($"no se pudo leer «{field}» de «{idContains}»");
            }

            return EvalCheck.From(value < ceiling, $"{field} = {value}");
        });

    public static EvalAssertion ProposedChoiceIsOneOf(string field, params string[] allowed) => new(
        $"{field} propuesto es uno de: {string.Join("/", allowed)}",
        ctx =>
        {
            var values = ctx.Result.ProposedEntities
                .Select(e => ReadRaw(e, field)?.ToString())
                .Where(v => v is not null)
                .ToList();
            if (values.Count == 0)
            {
                return EvalCheck.Fail($"ninguna entidad propuesta expone «{field}»");
            }

            var bad = values.Where(v => !allowed.Contains(v, StringComparer.OrdinalIgnoreCase)).ToList();
            return EvalCheck.From(bad.Count == 0, $"valores: {string.Join(", ", values)}");
        });

    public static EvalAssertion ReplyIsJsonOnly() => new(
        "la respuesta cruda es un unico objeto JSON, sin prosa ni fences",
        ctx =>
        {
            var text = ctx.RawReply.Trim();
            var clean = !text.Contains("```", StringComparison.Ordinal)
                        && text.StartsWith('{')
                        && text.EndsWith('}');
            return EvalCheck.From(clean, Excerpt(text));
        });

    public static EvalAssertion AnswerMentionsAnyOf(params string[] needles) => new(
        $"la respuesta menciona alguno de: {string.Join(", ", needles)}",
        ctx =>
        {
            var haystack = $"{ctx.Result.Answer} {ctx.Result.Rationale}";
            return EvalCheck.From(
                needles.Any(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase)),
                Excerpt(haystack));
        });

    public static EvalAssertion LatencyUnder(TimeSpan max) => new(
        $"la llamada tarda menos de {max.TotalSeconds:0.#}s",
        ctx => ctx.Call is null
            ? EvalCheck.Fail("sin traza de la llamada")
            : EvalCheck.From(ctx.Call.Duration < max, $"tardo {ctx.Call.Duration.TotalMilliseconds:0} ms"));

    public static EvalAssertion EstimatedCostUnder(decimal usd) => new(
        $"el costo estimado es menor a US$ {usd}",
        ctx => ctx.Call is null
            ? EvalCheck.Fail("sin traza de la llamada")
            : EvalCheck.From(ctx.Call.EstimatedCostUsd < usd, $"estimado US$ {ctx.Call.EstimatedCostUsd}"));

    private static ContentDatabase Merge(ContentDatabase existing, IEnumerable<ContentEntity> extra)
    {
        var db = new ContentDatabase();
        foreach (var entity in existing.All)
        {
            db.Add(entity);
        }

        foreach (var entity in extra)
        {
            db.Remove(entity.Id);
            db.Add(entity);
        }

        return db;
    }

    private static bool TryReadNumber(ContentEntity entity, string field, out double value)
    {
        var raw = ReadRaw(entity, field);
        if (raw is null)
        {
            value = 0;
            return false;
        }

        try
        {
            value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            value = 0;
            return false;
        }
    }

    private static object? ReadRaw(ContentEntity entity, string field)
    {
        var editable = EditableSchema.For(entity.GetType()).Fields
            .FirstOrDefault(f => string.Equals(f.PropertyName, field, StringComparison.OrdinalIgnoreCase));
        return editable?.GetValue(entity);
    }

    private static string Target(string idContains) =>
        string.IsNullOrEmpty(idContains) ? "de la entidad propuesta" : $"de «{idContains}»";

    private static string Excerpt(string text) =>
        text.Length <= 120 ? text : text[..120] + "…";

    [GeneratedRegex(@"^[a-z]+\.[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex BuildIdPattern();
}
