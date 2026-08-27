using Foundry.Core.Editing;

namespace Foundry.Core.Content;

/// <summary>Copia una entidad campo por campo usando su <see cref="EditableSchema"/>.</summary>
public static class ContentCloner
{
    public static ContentEntity Clone(ContentEntity source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var copy = (ContentEntity)Activator.CreateInstance(source.GetType())!;
        copy.Id = source.Id;

        foreach (var field in EditableSchema.For(source.GetType()).Fields)
        {
            field.SetValue(copy, field.GetValue(source));
        }

        return copy;
    }
}
