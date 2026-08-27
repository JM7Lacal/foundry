namespace Foundry.Presentation.Services;

/// <summary>Preguntas simples al usuario (confirmaciones, avisos). La implementacion la aporta la UI.</summary>
public interface IDialogService
{
    /// <summary>Pregunta Si/No. Devuelve <c>true</c> si el usuario confirma.</summary>
    bool Confirm(string message, string title);

    void Inform(string message, string title);
}
