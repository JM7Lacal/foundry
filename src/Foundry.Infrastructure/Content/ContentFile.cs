using Foundry.Core.Content;

namespace Foundry.Infrastructure.Content;

/// <summary>
/// Forma en disco de la base de contenido. Es un DTO interno: el formato del archivo es un
/// detalle de infraestructura, no se filtra al dominio ni a la capa de aplicacion.
/// </summary>
internal sealed class ContentFile
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Version del esquema del archivo, para migraciones futuras.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<ContentEntity> Entities { get; set; } = [];
}
