using System.Globalization;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Application.Validation;

/// <summary>Un problema encontrado en una entidad: campo y descripcion.</summary>
public sealed record ValidationIssue(EntityId EntityId, string EntityName, string Field, string Message)
{
    public override string ToString() => $"{EntityName} · {Field}: {Message}";
}

/// <summary>
/// Valida la base de contenido completa recorriendo el <see cref="EditableSchema"/> de cada
/// entidad y aplicando un conjunto de reglas: campos requeridos (<c>[Required]</c>), rangos
/// (<c>[Range]</c>) e integridad de referencias (toda referencia debe resolver dentro de la base).
/// Las reglas son una lista: agregar una (p. ej. coherencia de cadenas de mejora) no toca
/// <see cref="Validate"/>.
/// </summary>
public sealed class ContentValidator
{
    private delegate ValidationIssue? Rule(ContentEntity entity, EditableField field, object? value, ContentDatabase database);

    private readonly Rule[] _rules;

    public ContentValidator()
    {
        _rules = [CheckRequired, CheckRange, CheckReference];
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
                foreach (var rule in _rules)
                {
                    if (rule(entity, field, value, database) is { } issue)
                    {
                        issues.Add(issue);
                    }
                }
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
        return missing ? new ValidationIssue(entity.Id, entity.Name, field.Label, "es requerido") : null;
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
                $"fuera de rango [{field.Minimum}, {field.Maximum}] (es {number})");
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
            : new ValidationIssue(entity.Id, entity.Name, field.Label, $"referencia inexistente: {reference}");
    }

    private static bool TryToDouble(object value, out double result)
    {
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
