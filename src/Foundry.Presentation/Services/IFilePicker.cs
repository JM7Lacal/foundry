namespace Foundry.Presentation.Services;

/// <summary>
/// Abstraccion de los dialogos de archivo. El ViewModel no instancia <c>OpenFileDialog</c>
/// directamente: asi sigue siendo testeable y libre de dependencias de WPF.
/// </summary>
public interface IFilePicker
{
    /// <summary>Pide un archivo para abrir. Devuelve <c>null</c> si el usuario cancela.</summary>
    string? PickOpenFile(string filter);

    /// <summary>Pide una ruta para guardar. Devuelve <c>null</c> si el usuario cancela.</summary>
    string? PickSaveFile(string filter, string? suggestedFileName);
}
