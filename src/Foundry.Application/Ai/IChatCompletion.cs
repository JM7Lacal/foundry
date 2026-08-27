namespace Foundry.Application.Ai;

/// <summary>Un turno de la conversacion. <paramref name="Role"/>: "system" | "user" | "assistant".</summary>
public sealed record ChatMessage(string Role, string Content);

/// <summary>
/// Puerto generico hacia un modelo de lenguaje: mensajes entran, texto sale. Los providers
/// (Claude Code, Ollama, API hosteada, stub) implementan esto y no saben nada de Foundry.
/// Cambiar de proveedor = cambiar el registro en el composition root.
/// </summary>
public interface IChatCompletion
{
    /// <summary>Nombre corto del proveedor activo, para mostrar en la UI ("claude-code", "ollama:qwen2.5").</summary>
    string Name { get; }

    /// <exception cref="ChatCompletionException">El proveedor no esta disponible o fallo.</exception>
    Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}

/// <summary>Falla al hablar con el modelo: proveedor no instalado, timeout, respuesta invalida.</summary>
public sealed class ChatCompletionException : Exception
{
    public ChatCompletionException()
    {
    }

    public ChatCompletionException(string message)
        : base(message)
    {
    }

    public ChatCompletionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
