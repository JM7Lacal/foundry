using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foundry.Application.Content;
using Foundry.Application.Editing;
using Foundry.Application.Undo;
using Foundry.Application.Validation;
using Foundry.Core.Content;
using Foundry.Presentation.Services;

namespace Foundry.Presentation.ViewModels;

/// <summary>
/// ViewModel raiz. Orquesta carga/guardado, el arbol de categorias, el Inspector, el preview JSON,
/// el historial de undo/redo y el estado "cambios sin guardar".
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IContentRepository _repository;
    private readonly IContentSerializer _serializer;
    private readonly IFilePicker _filePicker;
    private readonly IDialogService _dialogs;
    private readonly UndoStack _undoStack;
    private readonly ContentValidator _validator;

    private ContentDatabase _database = new();

    [ObservableProperty]
    private string _jsonPreview = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Sin contenido cargado.";

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private object? _selectedTreeItem;

    [ObservableProperty]
    private bool _isDirty;

    public MainViewModel(
        IContentRepository repository,
        IContentSerializer serializer,
        IFilePicker filePicker,
        IDialogService dialogs,
        UndoStack undoStack,
        ContentValidator validator,
        InspectorViewModel inspector)
    {
        _repository = repository;
        _serializer = serializer;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _undoStack = undoStack;
        _validator = validator;
        Inspector = inspector;

        _undoStack.Changed += OnUndoStackChanged;
    }

    public ObservableCollection<ContentCategoryViewModel> Categories { get; } = [];

    public InspectorViewModel Inspector { get; }

    public string Title
    {
        get
        {
            var name = CurrentFilePath is null ? "Editor de contenido" : Path.GetFileName(CurrentFilePath);
            return $"Foundry — {name}{(IsDirty ? " *" : string.Empty)}";
        }
    }

    /// <summary>Entidad seleccionada en el arbol, o <c>null</c> si hay una categoria o nada.</summary>
    public ContentEntity? SelectedEntity => (SelectedTreeItem as EntityNodeViewModel)?.Entity;

    public async Task LoadFromAsync(string path)
    {
        StatusMessage = "Cargando...";

        try
        {
            _database = await _repository.LoadAsync(path).ConfigureAwait(true);
            CurrentFilePath = path;
            _undoStack.Clear();
            RebuildTree();
            IsDirty = false;
            StatusMessage = $"{_database.Count} entidades cargadas.";
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
        if (IsDirty && !_dialogs.Confirm(
                "Hay cambios sin guardar. ¿Descartarlos y abrir otro archivo?", "Foundry"))
        {
            return;
        }

        var path = _filePicker.PickOpenFile("Contenido de juego (*.json)|*.json");
        if (path is not null)
        {
            await LoadFromAsync(path).ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var issues = _validator.Validate(_database);
        if (issues.Count > 0)
        {
            StatusMessage = $"No se guardo: {issues.Count} problema(s) de validacion. Ej.: {issues[0]}";
            _dialogs.Inform(
                string.Join(Environment.NewLine, issues.Take(15).Select(issue => "• " + issue)),
                $"{issues.Count} problema(s) de validacion");
            return;
        }

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
            IsDirty = false;
            StatusMessage = $"Guardado en {path}";
        }
        catch (ContentRepositoryException ex)
        {
            StatusMessage = $"Error al guardar: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _undoStack.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _undoStack.Redo();

    private bool CanSave() => _database.Count > 0;

    private bool CanUndo() => _undoStack.CanUndo;

    private bool CanRedo() => _undoStack.CanRedo;

    partial void OnSelectedTreeItemChanged(object? value)
    {
        OnPropertyChanged(nameof(SelectedEntity));
        Inspector.Load(SelectedEntity, _database);
        UpdatePreview();
    }

    partial void OnCurrentFilePathChanged(string? value) => OnPropertyChanged(nameof(Title));

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Title));

    private void OnUndoStackChanged(object? sender, EventArgs e)
    {
        if (_undoStack.CanUndo || _undoStack.CanRedo)
        {
            IsDirty = true;
        }

        Inspector.RefreshValues();
        UpdatePreview();
        (SelectedTreeItem as EntityNodeViewModel)?.Refresh();

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();

        StatusMessage = Inspector.HasErrors ? "Cambios sin guardar — hay errores de validacion." : "Cambios sin guardar.";
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
