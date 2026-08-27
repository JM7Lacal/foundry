using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foundry.Presentation.Services;
using Foundry.Application.Content;
using Foundry.Application.Editing;
using Foundry.Core.Content;

namespace Foundry.Presentation.ViewModels;

/// <summary>
/// ViewModel raiz. Orquesta la carga/guardado del archivo de contenido, el arbol de categorias,
/// el Inspector de la entidad seleccionada y el preview JSON.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IContentRepository _repository;
    private readonly IFilePicker _filePicker;
    private readonly IContentSerializer _serializer;

    private ContentDatabase _database = new();

    [ObservableProperty]
    private string _jsonPreview = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Sin contenido cargado.";

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private object? _selectedTreeItem;

    public MainViewModel(
        IContentRepository repository,
        IFilePicker filePicker,
        IContentSerializer serializer,
        InspectorViewModel inspector)
    {
        _repository = repository;
        _filePicker = filePicker;
        _serializer = serializer;
        Inspector = inspector;
        Inspector.EntityEdited += OnEntityEdited;
    }

    public ObservableCollection<ContentCategoryViewModel> Categories { get; } = [];

    public InspectorViewModel Inspector { get; }

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

    partial void OnSelectedTreeItemChanged(object? value)
    {
        OnPropertyChanged(nameof(SelectedEntity));
        Inspector.Load(SelectedEntity, _database);
        UpdatePreview();
    }

    partial void OnCurrentFilePathChanged(string? value) => OnPropertyChanged(nameof(Title));

    private void OnEntityEdited(object? sender, EventArgs e)
    {
        UpdatePreview();
        (SelectedTreeItem as EntityNodeViewModel)?.Refresh();
        StatusMessage = "Cambios sin guardar.";
    }

    private void UpdatePreview() =>
        JsonPreview = SelectedEntity is null ? string.Empty : _serializer.SerializeEntity(SelectedEntity);

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
