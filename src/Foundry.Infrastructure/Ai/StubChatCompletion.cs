using Foundry.Application.Ai;

namespace Foundry.Infrastructure.Ai;

/// <summary>
/// Proveedor de respuestas armadas. No llama a ningun modelo: sirve para tests y para que el
/// panel funcione sin configurar nada. Se cambia por uno real en el composition root.
/// </summary>
public sealed class StubChatCompletion : IChatCompletion
{
    private const string CreatePrompt =
        """
        { "rationale": "Ejemplo del proveedor 'stub' — no es una respuesta real de un modelo. Configurá 'claude-code' u 'ollama' en appsettings.json.",
          "entities": [ { "$type": "troop", "id": "troop.stub-guardian", "name": "Guardian (stub)", "cost": 140, "damage": 22, "damageType": "physical", "attacksPerSecond": 0.9, "attackRange": 1.2, "health": 220, "armor": "heavy" } ] }
        """;

    private const string ReviewPrompt =
        """
        { "answer": "Proveedor 'stub': para el chequeo real de rangos, referencias y cadenas de mejora usá Herramientas -> Validar contenido. Configurá un modelo real en appsettings.json para respuestas generadas." }
        """;

    private const string Default =
        """
        { "answer": "Proveedor 'stub' activo (respuestas de ejemplo). Cambiá \"Assistant:Provider\" en appsettings.json a \"claude-code\" u \"ollama\"." }
        """;

    public string Name => "stub";

    public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var request = messages.LastOrDefault(m => m.Role == "user")?.Content.ToLowerInvariant() ?? string.Empty;

        var response = request switch
        {
            _ when ContainsAny(request, "crea", "generá", "genera", "personaje", "tropa", "torre", "enemigo", "unidad")
                => CreatePrompt,
            _ when ContainsAny(request, "revis", "consisten", "coheren", "balance", "nivel", "chequea")
                => ReviewPrompt,
            _ => Default,
        };

        return Task.FromResult(response);
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.Ordinal));
}
