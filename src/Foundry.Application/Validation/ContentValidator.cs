using System.Globalization;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Application.Validation;

public enum ValidationSeverity
{
    /// <summary>Rompe al juego al parsear (falta un requerido, valor fuera de rango, referencia rota).</summary>
    Error,

    /// <summary>Huele mal pero no rompe nada (incoherencia de balance). No bloquea el guardado.</summary>
    Warning,
}

/// <summary>Un problema encontrado en una entidad: campo, descripcion y gravedad.</summary>
public sealed record ValidationIssue(
    EntityId EntityId, string EntityName, string Field, string Message, ValidationSeverity Severity)
{
    public override string ToString()
    {
        var tag = Severity == ValidationSeverity.Warning ? "aviso" : "error";
        return $"[{tag}] {EntityName} · {Field}: {Message}";
    }
}

/// <summary>
/// Valida la base de contenido completa. Dos familias de reglas:
/// <list type="bullet">
///   <item><b>Por campo</b> (recorriendo el <see cref="EditableSchema"/>): requerido, rango,
///   integridad de referencias.</item>
///   <item><b>Por entidad</b>: coherencia de la cadena de mejora (una entidad no puede superar en
///   sus stats de progresion a la que declara como "mejora a", ni formar un ciclo).</item>
/// </list>
/// Cada familia es una lista: agregar una regla no toca <see cref="Validate"/>.
/// </summary>
public sealed class ContentValidator
{
    private delegate ValidationIssue? FieldRule(ContentEntity entity, EditableField field, object? value, ContentDatabase database);

    private delegate IEnumerable<ValidationIssue> EntityRule(ContentEntity entity, ContentDatabase database);

    private readonly FieldRule[] _fieldRules;
    private readonly EntityRule[] _entityRules;

    public ContentValidator()
    {
        _fieldRules = [CheckRequired, CheckRange, CheckReference];
        _entityRules = [CheckUpgradeChain];
    }

    public IReadOnlyList<ValidationIssue> Validate(ContentDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        var issues = new List<ValidationIssue>();

        foreach (var entity in database.All)
        {
            foreach (var field in EditableSchema.For(entity.GetType()).Fields)
            {
                var value = field.GetValue(entity);
                foreach (var rule in _fieldRules)
                {
                    if (rule(entity, field, value, database) is { } issue)
                    {
                        issues.Add(issue);
                    }
                }
            }

            foreach (var rule in _entityRules)
            {
                issues.AddRange(rule(entity, database));
            }
        }

        return issues;
    }

    private static ValidationIssue? CheckRequired(
        ContentEntity entity, EditableField field, object? value, ContentDatabase database)
    {
        if (!field.IsRequired)
        {
            return null;
        }

        var missing = value is null || (value is string text && string.IsNullOrWhiteSpace(text));
        return missing
            ? new ValidationIssue(entity.Id, entity.Name, field.Label, "es requerido", ValidationSeverity.Error)
            : null;
    }

    private static ValidationIssue? CheckRange(
        ContentEntity entity, EditableField field, object? value, ContentDatabase database)
    {
        if (!field.HasRange || value is null || !TryToDouble(value, out var number))
        {
            return null;
        }

        if (number < field.Minimum || number > field.Maximum)
        {
            return new ValidationIssue(
                entity.Id,
                entity.Name,
                field.Label,
                $"fuera de rango [{field.Minimum}, {field.Maximum}] (es {number})",
                ValidationSeverity.Error);
        }

        return null;
    }

    private static ValidationIssue? CheckReference(
        ContentEntity entity, EditableField field, object? value, ContentDatabase database)
    {
        if (field.Kind != FieldKind.Reference || value is not EntityId reference)
        {
            return null;
        }

        return database.Contains(reference)
            ? null
            : new ValidationIssue(
                entity.Id, entity.Name, field.Label, $"referencia inexistente: {reference}", ValidationSeverity.Error);
    }

    private static IEnumerable<ValidationIssue> CheckUpgradeChain(ContentEntity entity, ContentDatabase database)
    {
        var schema = EditableSchema.For(entity.GetType());

        foreach (var referenceField in schema.Fields.Where(field => field.Kind == FieldKind.Reference))
        {
            if (referenceField.GetValue(entity) is not EntityId targetId)
            {
                continue;
            }

            var target = database.Find(targetId);
            if (target is null || target.GetType() != entity.GetType())
            {
                continue; // referencia rota o a otro tipo: lo cubren otras reglas
            }

            if (FormsCycle(entity, referenceField, database))
            {
                yield return new ValidationIssue(
                    entity.Id, entity.Name, referenceField.Label, "la cadena de mejora es circular",
                    ValidationSeverity.Error);
                continue;
            }

            var targetSchema = EditableSchema.For(target.GetType());
            foreach (var stat in schema.Fields.Where(field => field.IsProgression))
            {
                var targetStat = targetSchema.Fields.FirstOrDefault(f => f.PropertyName == stat.PropertyName);
                if (targetStat is null
                    || !TryToDouble(stat.GetValue(entity), out var baseValue)
                    || !TryToDouble(targetStat.GetValue(target), out var upgradeValue))
                {
                    continue;
                }

                if (baseValue > upgradeValue)
                {
                    yield return new ValidationIssue(
                        entity.Id,
                        entity.Name,
                        stat.Label,
                        $"supera a su mejora ({target.Name}): {Format(baseValue)} > {Format(upgradeValue)}",
                        ValidationSeverity.Warning);
                }
            }
        }
    }

    private static bool FormsCycle(ContentEntity start, EditableField referenceField, ContentDatabase database)
    {
        var seen = new HashSet<EntityId> { start.Id };
        var currentId = (EntityId?)referenceField.GetValue(start);

        while (currentId is { } id)
        {
            if (!seen.Add(id))
            {
                return true;
            }

            var current = database.Find(id);
            if (current is null || current.GetType() != start.GetType())
            {
                return false;
            }

            currentId = EditableSchema.For(current.GetType())
                .Fields.First(f => f.PropertyName == referenceField.PropertyName)
                .GetValue(current) as EntityId?;
        }

        return false;
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.CurrentCulture);

    private static bool TryToDouble(object? value, out double result)
    {
        if (value is null)
        {
            result = 0;
            return false;
        }

        try
        {
            result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            result = 0;
            return false;
        }
    }
}
