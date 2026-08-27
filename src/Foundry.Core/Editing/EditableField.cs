using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Foundry.Core.Content;

namespace Foundry.Core.Editing;

/// <summary>
/// Descriptor de una propiedad editable de una entidad: que etiqueta mostrar, en que grupo,
/// que tipo de control usar y como leer/escribir el valor. Lo produce <see cref="EditableSchema"/>
/// por reflexion; el Inspector no conoce ninguna entidad concreta.
/// </summary>
public sealed class EditableField
{
    private readonly PropertyInfo _property;

    internal EditableField(PropertyInfo property, EditablePropertyAttribute attribute)
    {
        _property = property;
        Label = attribute.Label ?? property.Name;
        Group = attribute.Group;
        Order = attribute.Order;
        Description = attribute.Description;

        var reference = property.GetCustomAttribute<AssetReferenceAttribute>();
        ReferenceTargetType = reference?.TargetType;
        Kind = DetermineKind(property.PropertyType, reference is not null);

        if (Kind == FieldKind.Choice)
        {
            EnumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }

        var range = property.GetCustomAttribute<RangeAttribute>();
        if (range is not null)
        {
            Minimum = ToDouble(range.Minimum);
            Maximum = ToDouble(range.Maximum);
        }

        IsRequired = property.GetCustomAttribute<RequiredAttribute>() is not null;
    }

    public string Label { get; }

    public string Group { get; }

    public int Order { get; }

    public string? Description { get; }

    public FieldKind Kind { get; }

    /// <summary>Tipo del enum cuando <see cref="Kind"/> es <see cref="FieldKind.Choice"/>.</summary>
    public Type? EnumType { get; }

    /// <summary>Tipo de entidad referenciada cuando <see cref="Kind"/> es <see cref="FieldKind.Reference"/>.</summary>
    public Type? ReferenceTargetType { get; }

    public double? Minimum { get; }

    public double? Maximum { get; }

    public bool HasRange => Minimum is not null && Maximum is not null;

    /// <summary>La propiedad esta marcada con <see cref="RequiredAttribute"/>.</summary>
    public bool IsRequired { get; }

    public string PropertyName => _property.Name;

    public object? GetValue(ContentEntity entity) => _property.GetValue(entity);

    public void SetValue(ContentEntity entity, object? value) => _property.SetValue(entity, value);

    private static FieldKind DetermineKind(Type propertyType, bool isReference)
    {
        if (isReference)
        {
            return FieldKind.Reference;
        }

        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (type == typeof(bool))
        {
            return FieldKind.Toggle;
        }

        if (type.IsEnum)
        {
            return FieldKind.Choice;
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
        {
            return FieldKind.WholeNumber;
        }

        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            return FieldKind.Number;
        }

        return FieldKind.Text;
    }

    private static double ToDouble(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);
}
