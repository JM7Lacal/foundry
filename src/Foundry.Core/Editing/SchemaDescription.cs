using System.Text;
using Foundry.Core.Content;

namespace Foundry.Core.Editing;

/// <summary>
/// Describe en texto compacto todos los tipos de entidad y sus campos editables. Lo consume el
/// asistente para decirle al modelo que JSON puede producir — el mismo esquema que arma el
/// Inspector arma el prompt.
/// </summary>
public static class SchemaDescription
{
    public static string ForPrompt()
    {
        var builder = new StringBuilder();

        foreach (var entityType in ContentEntityCatalog.Types)
        {
            builder.Append("- \"").Append(ContentEntityCatalog.DiscriminatorFor(entityType)).Append("\": ");
            var fields = EditableSchema.For(entityType).Fields
                .Select(DescribeField);
            builder.AppendLine(string.Join(", ", fields));
        }

        return builder.ToString().TrimEnd();
    }

    private static string DescribeField(EditableField field)
    {
        var type = field.Kind switch
        {
            FieldKind.WholeNumber => field.HasRange ? $"entero {field.Minimum:0}..{field.Maximum:0}" : "entero",
            FieldKind.Number => field.HasRange ? $"decimal {field.Minimum:0.##}..{field.Maximum:0.##}" : "decimal",
            FieldKind.Toggle => "true/false",
            FieldKind.Choice => string.Join("|", Enum.GetNames(field.EnumType!)).ToLowerInvariant(),
            FieldKind.Reference => $"id de {ContentEntityCatalog.DiscriminatorFor(field.ReferenceTargetType!)}",
            _ => "texto",
        };

        var flags = field.IsRequired ? " (requerido)" : string.Empty;
        return $"{field.PropertyName} [{type}]{flags}";
    }
}
