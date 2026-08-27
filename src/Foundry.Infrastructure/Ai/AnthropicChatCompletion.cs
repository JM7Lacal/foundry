using System.Net.Http.Json;
using System.Text.Json;
using Foundry.Application.Ai;

namespace Foundry.Infrastructure.Ai;

/// <summary>
/// Habla con la API de Anthropic (<c>/v1/messages</c>). Requiere una API key. Costo por llamada
/// en centavos con un modelo Haiku; mejor calidad que un modelo local.
/// </summary>
public sealed class AnthropicChatCompletion : IChatCompletion
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public AnthropicChatCompletion(HttpClient http, string apiKey, string model)
    {
        _http = http;
        _apiKey = apiKey;
        _model = model;
    }

    public string Name => $"anthropic:{_model}";

    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new ChatCompletionException("Falta la API key de Anthropic (Assistant:ApiKey en appsettings.json).");
        }

        var system = string.Join("\n\n", messages.Where(m => m.Role == "system").Select(m => m.Content));
        var turns = messages
            .Where(m => m.Role is "user" or "assistant")
            .Select(m => new { role = m.Role, content = m.Content })
            .ToArray();

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                model = _model,
                max_tokens = 2048,
                system,
                messages = turns,
            }),
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new ChatCompletionException("No se pudo conectar con la API de Anthropic.", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new ChatCompletionException("La API de Anthropic tardo demasiado.", ex);
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ChatCompletionException($"Anthropic respondio {(int)response.StatusCode}: {payload}");
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.GetProperty("content")[0].GetProperty("text").GetString()
                   ?? throw new ChatCompletionException("Respuesta de Anthropic sin texto.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException)
        {
            throw new ChatCompletionException("No se pudo interpretar la respuesta de Anthropic.", ex);
        }
    }
}
