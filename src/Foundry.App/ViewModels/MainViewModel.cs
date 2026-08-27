using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foundry.App.Services;
using Foundry.Application.Content;
using Foundry.Core.Content;

namespace Foundry.App.ViewModels;

/// <summary>
/// ViewModel raiz. Orquesta la carga/guardado del archivo de contenido, el arbol de categorias
/// y que entidad esta seleccionada para el Inspector.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IContentRepository _repository;
    private readonly IFilePicker _filePicker;

    private ContentDatabase _database = new();

    [ObservableProperty]
    private string _statusMessage = "Sin contenido cargado.";

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private object? _selectedTreeItem;

    public MainViewModel(IContentRepository repository, IFilePicker filePicker)
    {
        _repository = repository;
        _filePicker = filePicker;
    }

    public ObservableCollection<ContentCategoryViewModel> Categories { get; } = [];

    public string Title => CurrentFilePath is null
        ? "Foundry — Editor de contenido"
        : $"Foundry — {System.IO.Path.GetFileName(CurrentFilePath)}";

    /// <summary>Entidad seleccionada en el arbol, o <c>null</c> si hay una categoria o nada.</summary>
    public ContentEntity? SelectedEntity => (SelectedTreeItem as EntityNodeViewModel)?.Entity;

    public async Task LoadFromAsync(string path)
    {
        StatusMessage = "Cargando...";

        try
        {
            var database = await _repository.LoadAsync(path).ConfigureAwait(true);
            _database = database;
            CurrentFilePath = path;
            RebuildTree();
            StatusMessage = $"{database.Count} entidades cargadas.";
        }
        catch (ContentRepositoryException ex)
        {
            StatusMessage = $"No se pudo cargar: {ex.Message}";
        }

        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        var path = _filePicker.PickOpenFile("Contenido de juego (*.json)|*.json");
        if (path is not null)
        {
            await LoadFromAsync(path).ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var path = CurrentFilePath
                   ?? _filePicker.PickSaveFile("Contenido de juego (*.json)|*.json", "contenido.json");
        if (path is null)
        {
            return;
        }

        try
        {
            await _repository.SaveAsync(_database, path).ConfigureAwait(true);
            CurrentFilePath = path;
            StatusMessage = $"Guardado en {path}";
        }
        catch (ContentRepositoryException ex)
        {
            StatusMessage = $"Error al guardar: {ex.Message}";
        }
    }

    private bool CanSave() => _database.Count > 0;

    partial void OnSelectedTreeItemChanged(object? value) => OnPropertyChanged(nameof(SelectedEntity));

    partial void OnCurrentFilePathChanged(string? value) => OnPropertyChanged(nameof(Title));

    private void RebuildTree()
    {
        Categories.Clear();

        var groups = _database.All
            .GroupBy(entity => entity.CategoryName, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.CurrentCulture);

        foreach (var group in groups)
        {
            var category = new ContentCategoryViewModel(group.Key);
            foreach (var entity in group.OrderBy(e => e.Name, StringComparer.CurrentCulture))
            {
                category.Entities.Add(new EntityNodeViewModel(entity));
            }

            Categories.Add(category);
        }
    }
}
