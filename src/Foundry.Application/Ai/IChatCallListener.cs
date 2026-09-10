namespace Foundry.Application.Ai;

/// <summary>
/// Recibe una <see cref="ChatCallReport"/> por cada llamada al modelo. Es el punto de enganche
/// para observabilidad: escribir un log, alimentar un panel, exportar metricas. Las
/// implementaciones deben ser rapidas y no lanzar (la llamada al modelo ya termino).
/// </summary>
public interface IChatCallListener
{
    void OnCall(ChatCallReport report);
}

/// <summary>Descarta todas las trazas. Es el listener por defecto cuando no se configura otro.</summary>
public sealed class NullChatCallListener : IChatCallListener
{
    public static NullChatCallListener Instance { get; } = new();

    public void OnCall(ChatCallReport report)
    {
        // sin observabilidad configurada
    }
}
