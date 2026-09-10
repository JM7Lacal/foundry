using System.Globalization;
using System.Text;
using System.Text.Json;
using Foundry.Application.Editing;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Application.Ai;

/// <summary>
/// Lo que devuelve el asistente: una respuesta de texto y/o entidades propuestas. <c>PromptId</c>
/// identifica la version de system prompt que produjo la respuesta (para evals y trazas).
/// </summary>
public sealed record AssistantResult(
    string? Answer,
    string? Rationale,
    IReadOnlyList<ContentEntity> ProposedEntities,
    string PromptId = "")
{
    public bool HasProposal => ProposedEntities.Count > 0;
}

/// <summary>
/// El "cerebro" del asistente. Arma el prompt con el esquema y el contexto de la base, llama al
/// <see cref="IChatCompletion"/> (cualquier proveedor) y parsea la respuesta a entidades validas.
/// Esta clase NO cambia al cambiar de modelo.
/// </summary>
public sealed class ContentAssistant
{
    private readonly IChatCompletion _chat;
    private readonly IContentSerializer _serializer;
    private readonly PromptTemplate _systemPrompt;

    /// <summary>
    /// <paramref name="prompts"/>: biblioteca de system prompts (por defecto la incrustada).
    /// <paramref name="promptVersion"/>: id (<c>assistant-system@v1</c>) o familia
    /// (<c>assistant-system</c> → ultima); por defecto, la ultima.
    /// </summary>
    public ContentAssistant(
        IChatCompletion chat,
        IContentSerializer serializer,
        PromptLibrary? prompts = null,
        string? promptVersion = null)
    {
        _chat = chat;
        _serializer = serializer;
        _systemPrompt = (prompts ?? PromptLibrary.Default)
            .Resolve(string.IsNullOrWhiteSpace(promptVersion) ? "assistant-system" : promptVersion);
    }

    public string ProviderName => _chat.Name;

    /// <summary>Id de la version de system prompt en uso (p. ej. <c>assistant-system@v2</c>).</summary>
    public string PromptId => _systemPrompt.Id;

    public async Task<AssistantResult> AskAsync(string request, ContentDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        ArgumentNullException.ThrowIfNull(database);

        var messages = new ChatMessage[]
        {
            new("system", SystemPrompt()),
            new("user", UserPrompt(request, database)),
        };

        var raw = await _chat.CompleteAsync(messages, cancellationToken).ConfigureAwait(false);
        return Parse(raw);
    }

    private string SystemPrompt() =>
        _systemPrompt.Render(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["schema"] = SchemaDescription.ForPrompt(),
        });

    private static string UserPrompt(string request, ContentDatabase database)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Contenido existente (resumen):");

        foreach (var entity in database.All.OrderBy(e => e.Id.Value, StringComparer.Ordinal))
        {
            builder.Append("- ").Append(ContentEntityCatalog.DiscriminatorFor(entity.GetType()))
                .Append(' ').Append(entity.Id).Append(" \"").Append(entity.Name).Append("\": ")
                .AppendLine(KeyStats(entity));
        }

        builder.AppendLine().Append("Pedido: ").Append(request);
        return builder.ToString();
    }

    private static string KeyStats(ContentEntity entity)
    {
        var stats = EditableSchema.For(entity.GetType()).Fields
            .Where(f => f.Kind is FieldKind.WholeNumber or FieldKind.Number or FieldKind.Choice)
            .Select(f => $"{f.PropertyName}={Format(f.GetValue(entity))}");
        return string.Join(", ", stats);
    }

    private static string Format(object? value) => value switch
    {
        null => "?",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "?",
    };

    private AssistantResult Parse(string raw)
    {
        var json = ExtractJson(raw);

        string? answer = null;
        string? rationale = null;
        var parsedAsJson = false;

        try
        {
            using var document = JsonDocument.Parse(json);
            parsedAsJson = true;
            var root = document.RootElement;
            if (root.TryGetProperty("answer", out var a) && a.ValueKind == JsonValueKind.String)
            {
                answer = a.GetString();
            }

            if (root.TryGetProperty("rationale", out var r) && r.ValueKind == JsonValueKind.String)
            {
                rationale = r.GetString();
            }
        }
        catch (JsonException)
        {
            // el modelo no respeto el formato: tratamos la respuesta como texto plano
        }

        var entities = parsedAsJson && HasEntities(json)
            ? _serializer.DeserializeEntities(json)
            : Array.Empty<ContentEntity>();

        if (answer is null && rationale is null && entities.Count == 0)
        {
            answer = raw.Trim();
        }

        return new AssistantResult(answer, rationale, entities, _systemPrompt.Id);
    }

    private static bool HasEntities(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("entities", out var e)
                   && e.ValueKind == JsonValueKind.Array
                   && e.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ExtractJson(string raw)
    {
        var text = raw.Trim();

        // ```json ... ```  o  ``` ... ```
        var fence = text.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            var start = text.IndexOf('\n', fence);
            var end = text.LastIndexOf("```", StringComparison.Ordinal);
            if (start > 0 && end > start)
            {
                text = text[(start + 1)..end].Trim();
            }
        }

        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace ? text[firstBrace..(lastBrace + 1)] : text;
    }
}
