using CommunityToolkit.Mvvm.ComponentModel;

namespace Foundry.App.ViewModels;

/// <summary>
/// ViewModel raiz de la ventana principal. Por ahora solo expone titulo y mensaje de estado;
/// en el Dia 1 pasa a orquestar el arbol de contenido, el inspector y el preview.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusMessage = "Listo.";

    public string Title { get; } = "Foundry — Editor de contenido";
}
