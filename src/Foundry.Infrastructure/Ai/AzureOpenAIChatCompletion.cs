using System.Net.Http.Json;
using System.Text.Json;
using Foundry.Application.Ai;

namespace Foundry.Infrastructure.Ai;

/// <summary>
/// Habla con un modelo desplegado en Azure OpenAI / Azure AI Foundry (endpoint <c>chat/completions</c>
/// de un <em>deployment</em>). Requiere endpoint del recurso, nombre del deployment y una API key.
/// Misma forma que <see cref="AnthropicChatCompletion"/>: mensajes entran, texto sale.
/// </summary>
/// <remarks>
/// Config (<c>appsettings.Local.json</c>): <c>Assistant:Provider = "azure"</c>,
/// <c>Assistant:Azure:Endpoint</c> (p. ej. <c>https://mi-recurso.openai.azure.com</c>),
/// <c>Assistant:Azure:Deployment</c>, <c>Assistant:ApiKey</c>. La <c>ApiVersion</c> es opcional.
/// </remarks>
public sealed class AzureOpenAIChatCompletion : IChatCompletion
{
    private const string DefaultApiVersion = "2024-10-21";

    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _deployment;
    private readonly string _apiKey;
    private readonly string _apiVersion;
    private readonly string _label;

    public AzureOpenAIChatCompletion(
        HttpClient http,
        string endpoint,
        string deployment,
        string apiKey,
        string? apiVersion = null,
        string? model = null)
    {
        _http = http;
        _endpoint = (endpoint ?? string.Empty).TrimEnd('/');
        _deployment = deployment ?? string.Empty;
        _apiKey = apiKey ?? string.Empty;
        _apiVersion = string.IsNullOrWhiteSpace(apiVersion) ? DefaultApiVersion : apiVersion;
        _label = string.IsNullOrWhiteSpace(model) ? _deployment : model;
    }

    public string Name => $"azure:{_label}";

    public async Task<string> CompleteAsync(
        IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (string.IsNullOrWhiteSpace(_endpoint) || string.IsNullOrWhiteSpace(_deployment))
        {
            throw new ChatCompletionException(
                "Falta configurar Azure: Assistant:Azure:Endpoint y Assistant:Azure:Deployment.");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new ChatCompletionException("Falta la API key de Azure (Assistant:ApiKey).");
        }

        var url = $"{_endpoint}/openai/deployments/{_deployment}/chat/completions?api-version={_apiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                max_tokens = 2048,
                temperature = 0.2,
            }),
        };
        request.Headers.Add("api-key", _apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new ChatCompletionException("No se pudo conectar con Azure OpenAI.", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new ChatCompletionException("Azure OpenAI tardo demasiado.", ex);
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ChatCompletionException($"Azure OpenAI respondio {(int)response.StatusCode}: {payload}");
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(content)
                ? throw new ChatCompletionException("Azure OpenAI devolvio una respuesta vacia.")
                : content;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException)
        {
            throw new ChatCompletionException("No se pudo interpretar la respuesta de Azure OpenAI.", ex);
        }
    }
}
