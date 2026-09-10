using Foundry.Application.Ai;
using Foundry.Infrastructure.Ai;

namespace Foundry.Evals;

/// <summary>
/// Traduce un nombre de proveedor a la fabrica que usa <see cref="EvalRunner"/>. Las claves y
/// endpoints se leen de variables de entorno, con los mismos nombres que la app
/// (<c>Assistant__ApiKey</c>, <c>Assistant__Azure__Endpoint</c>, ...).
/// </summary>
public static class EvalProviders
{
    public static Func<EvalCase, IChatCompletion> For(string name)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "recorded":
                return c => RecordedChat.ForCase(c.Name);

            case "stub":
                return _ => new StubChatCompletion();

            case "claude-code":
            case "claudecode":
                return _ => new ClaudeCodeChatCompletion(Env("Assistant__Command"));

            case "anthropic":
            {
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                var key = Env("Assistant__ApiKey") ?? string.Empty;
                var model = Env("Assistant__Model") ?? "claude-haiku-4-5-20251001";
                return _ => new AnthropicChatCompletion(http, key, model);
            }

            case "ollama":
            {
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
                var model = Env("Assistant__Model") ?? "qwen2.5";
                return _ => new OllamaChatCompletion(http, model);
            }

            case "azure":
            case "azure-openai":
            case "foundry":
            {
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                var endpoint = Env("Assistant__Azure__Endpoint") ?? string.Empty;
                var deployment = Env("Assistant__Azure__Deployment") ?? string.Empty;
                var key = Env("Assistant__ApiKey") ?? string.Empty;
                var apiVersion = Env("Assistant__Azure__ApiVersion");
                var model = Env("Assistant__Model");
                return _ => new AzureOpenAIChatCompletion(http, endpoint, deployment, key, apiVersion, model);
            }

            default:
                throw new ArgumentException($"Proveedor de eval desconocido: '{name}'.", nameof(name));
        }
    }

    private static string? Env(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
