using System.Globalization;
using System.Text;
using Foundry.Application.Content;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Infrastructure.Content;

/// <summary>
/// Importa entidades desde un CSV con cabecera. Columnas obligatorias: <c>type</c> (discriminador,
/// p. ej. "troop") e <c>id</c>. El resto de las columnas se mapean a propiedades editables por
/// nombre o etiqueta (sin distinguir mayusculas), reusando el <see cref="EditableSchema"/>.
/// Agregar una entidad nueva no toca este importador.
/// </summary>
public sealed class CsvContentImporter : IContentImporter
{
    public string FormatName => "CSV";

    public string FileFilter => "Planillas CSV (*.csv)|*.csv";

    public IReadOnlyList<ContentEntity> Import(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new ContentImportException($"No existe el archivo '{path}'.");
        }

        using var reader = new StreamReader(path, Encoding.UTF8);
        var rows = ReadRows(reader);
        if (rows.Count == 0)
        {
            throw new ContentImportException("El archivo esta vacio.");
        }

        var header = rows[0];
        var typeIndex = IndexOf(header, "type");
        var idIndex = IndexOf(header, "id");
        if (typeIndex < 0 || idIndex < 0)
        {
            throw new ContentImportException("La cabecera debe incluir las columnas 'type' e 'id'.");
        }

        var entities = new List<ContentEntity>();
        for (var line = 1; line < rows.Count; line++)
        {
            entities.Add(BuildEntity(rows[line], header, typeIndex, idIndex, line + 1));
        }

        return entities;
    }

    private static ContentEntity BuildEntity(
        List<string> row, List<string> header, int typeIndex, int idIndex, int lineNumber)
    {
        var discriminator = Cell(row, typeIndex);
        var entityType = ContentEntityCatalog.Resolve(discriminator)
            ?? throw new ContentImportException($"Linea {lineNumber}: tipo desconocido '{discriminator}'.");

        var entity = (ContentEntity)Activator.CreateInstance(entityType)!;
        entity.Id = new EntityId(Require(row, idIndex, lineNumber, "id"));

        var fieldsByKey = FieldLookup(entityType);

        for (var column = 0; column < header.Count; column++)
        {
            if (column == typeIndex || column == idIndex)
            {
                continue;
            }

            var raw = Cell(row, column);
            if (raw.Length == 0 || !fieldsByKey.TryGetValue(Normalize(header[column]), out var field))
            {
                continue;
            }

            try
            {
                field.SetValue(entity, Coerce(raw, field.ValueType));
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
            {
                throw new ContentImportException(
                    $"Linea {lineNumber}, columna '{header[column]}': no se pudo interpretar '{raw}'.", ex);
            }
        }

        return entity;
    }

    private static Dictionary<string, EditableField> FieldLookup(Type entityType)
    {
        var lookup = new Dictionary<string, EditableField>(StringComparer.Ordinal);
        foreach (var field in EditableSchema.For(entityType).Fields)
        {
            lookup[Normalize(field.PropertyName)] = field;
            lookup[Normalize(field.Label)] = field;
        }

        return lookup;
    }

    private static object? Coerce(string raw, Type targetType)
    {
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (type == typeof(string))
        {
            return raw;
        }

        if (type == typeof(EntityId))
        {
            return new EntityId(raw);
        }

        if (type == typeof(bool))
        {
            return bool.Parse(raw);
        }

        if (type.IsEnum)
        {
            return Enum.Parse(type, raw, ignoreCase: true);
        }

        if (type == typeof(int))
        {
            return int.Parse(raw, CultureInfo.InvariantCulture);
        }

        if (type == typeof(long))
        {
            return long.Parse(raw, CultureInfo.InvariantCulture);
        }

        if (type == typeof(double))
        {
            return double.Parse(raw, CultureInfo.InvariantCulture);
        }

        if (type == typeof(float))
        {
            return float.Parse(raw, CultureInfo.InvariantCulture);
        }

        throw new ContentImportException($"Tipo de columna no soportado por el importador CSV: {type.Name}.");
    }

    private static int IndexOf(List<string> header, string name)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (Normalize(header[i]) == name)
            {
                return i;
            }
        }

        return -1;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static string Cell(List<string> row, int index) => index < row.Count ? row[index].Trim() : string.Empty;

    private static string Require(List<string> row, int index, int lineNumber, string column)
    {
        var value = Cell(row, index);
        return value.Length > 0
            ? value
            : throw new ContentImportException($"Linea {lineNumber}: la columna '{column}' no puede estar vacia.");
    }

    private static List<List<string>> ReadRows(TextReader reader)
    {
        var rows = new List<List<string>>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            rows.Add(ParseLine(line));
        }

        return rows;
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
