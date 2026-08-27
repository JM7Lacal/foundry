using System.Text.Json;
using Foundry.Application.Content;
using Foundry.Core.Content;
using Foundry.Infrastructure.Json;

namespace Foundry.Infrastructure.Content;

/// <summary>
/// <see cref="IContentRepository"/> sobre un unico archivo JSON. IO asincronica; nunca bloquea
/// el hilo llamador (el de UI).
/// </summary>
public sealed class JsonContentRepository : IContentRepository
{
    private readonly JsonSerializerOptions _options = FoundryJsonOptions.Create();

    public async Task<ContentDatabase> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new ContentRepositoryException($"No existe el archivo de contenido '{path}'.");
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var file = await JsonSerializer
                .DeserializeAsync<ContentFile>(stream, _options, cancellationToken)
                .ConfigureAwait(false);

            if (file is null)
            {
                throw new ContentRepositoryException($"El archivo '{path}' esta vacio.");
            }

            var database = new ContentDatabase();
            foreach (var entity in file.Entities)
            {
                database.Add(entity);
            }

            return database;
        }
        catch (JsonException ex)
        {
            throw new ContentRepositoryException($"El contenido de '{path}' no es JSON valido: {ex.Message}", ex);
        }
        catch (ArgumentException ex)
        {
            throw new ContentRepositoryException($"Contenido inconsistente en '{path}': {ex.Message}", ex);
        }
    }

    public async Task SaveAsync(ContentDatabase database, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        var file = new ContentFile
        {
            SchemaVersion = ContentFile.CurrentSchemaVersion,
            Entities = database.All
                .OrderBy(e => e.Id.Value, StringComparer.Ordinal)
                .ToList(),
        };

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(path);
            await JsonSerializer
                .SerializeAsync(stream, file, _options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            throw new ContentRepositoryException($"No se pudo escribir '{path}': {ex.Message}", ex);
        }
    }
}
