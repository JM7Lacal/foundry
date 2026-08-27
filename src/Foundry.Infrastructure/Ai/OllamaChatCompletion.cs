using System.Net.Http.Json;
using Foundry.Application.Ai;

namespace Foundry.Infrastructure.Ai;

/// <summary>
/// Habla con un modelo local via Ollama (<c>http://localhost:11434</c>). Gratis y privado, pero
/// depende del hardware. Le pide al modelo salida en JSON (<c>format: "json"</c>).
/// </summary>
public sealed class OllamaChatCompletion : IChatCompletion
{
    private readonly HttpClient _http;
    private readonly string _model;

    public OllamaChatCompletion(HttpClient http, string model)
    {
        _http = http;
        _model = model;
    }

    public string Name => $"ollama:{_model}";

    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var payload = new OllamaRequest(
            _model,
            messages.Select(m => new OllamaMessage(m.Role, m.Content)).ToList(),
            Stream: false,
            Format: "json",
            Options: new OllamaOptions(Temperature: 0.2));

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("http://localhost:11434/api/chat", payload, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new ChatCompletionException("No se pudo conectar a Ollama (localhost:11434). ¿Esta corriendo?", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new ChatCompletionException("Ollama tardo demasiado en responder.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ChatCompletionException($"Ollama respondio {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken).ConfigureAwait(false);
        return body?.Message?.Content
               ?? throw new ChatCompletionException("Ollama devolvio una respuesta vacia.");
    }

    // PostAsJsonAsync/ReadFromJsonAsync usan JsonSerializerDefaults.Web (camelCase, case-insensitive).
    private sealed record OllamaRequest(
        string Model, IReadOnlyList<OllamaMessage> Messages, bool Stream, string Format, OllamaOptions Options);

    private sealed record OllamaMessage(string Role, string Content);

    private sealed record OllamaOptions(double Temperature);

    private sealed record OllamaResponse(OllamaMessage? Message);
}
