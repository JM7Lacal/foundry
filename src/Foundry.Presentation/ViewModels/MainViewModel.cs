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
    private readonly IContentImporter _importer;
    private readonly IFilePicker _filePicker;
    private readonly IDialogService _dialogs;
    private readonly UndoStack _undoStack;
    private readonly ContentValidator _validator;

    private ContentDatabase _database = new();

    [ObservableProperty]
    private int _entityCount;

    [ObservableProperty]
    private bool _treeIsEmpty = true;

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

    [ObservableProperty]
    private string _searchText = string.Empty;

    public MainViewModel(
        IContentRepository repository,
        IContentSerializer serializer,
        IContentImporter importer,
        IFilePicker filePicker,
        IDialogService dialogs,
        UndoStack undoStack,
        ContentValidator validator,
        InspectorViewModel inspector,
        AssistantViewModel assistant)
    {
        _repository = repository;
        _serializer = serializer;
        _importer = importer;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _undoStack = undoStack;
        _validator = validator;
        Inspector = inspector;
        Assistant = assistant;

        _undoStack.Changed += OnUndoStackChanged;
        Assistant.ProposalApplied += OnAssistantProposalApplied;
    }

    public ObservableCollection<ContentCategoryViewModel> Categories { get; } = [];

    public InspectorViewModel Inspector { get; }

    public AssistantViewModel Assistant { get; }

    /// <summary>Tipos de entidad para el menu "Nueva entidad". Se arma solo del catalogo.</summary>
    public IReadOnlyList<EntityTypeOption> EntityTypes { get; } = ContentEntityCatalog.Types
        .Select(type => new EntityTypeOption(
            ContentEntityCatalog.DiscriminatorFor(type),
            ((ContentEntity)Activator.CreateInstance(type)!).CategoryName))
        .ToList();

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
            Assistant.SetContext(_database);
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
        var errors = _validator.Validate(_database)
            .Where(issue => issue.Severity == ValidationSeverity.Error)
            .ToList();
        if (errors.Count > 0)
        {
            StatusMessage = $"No se guardo: {errors.Count} error(es) de validacion. Ej.: {errors[0]}";
            _dialogs.Inform(
                string.Join(Environment.NewLine, errors.Take(15).Select(issue => "• " + issue)),
                $"{errors.Count} error(es) de validacion");
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

    [RelayCommand]
    private void NewEntity(string? discriminator)
    {
        var type = discriminator is null ? null : ContentEntityCatalog.Resolve(discriminator);
        if (type is null)
        {
            return;
        }

        var entity = (ContentEntity)Activator.CreateInstance(type)!;
        entity.Id = NextId(discriminator!);

        _undoStack.Execute(new AddEntitiesAction(_database, [entity]));
        RebuildTree();
        SelectEntity(entity.Id);
        StatusMessage = $"Nueva entidad {entity.Id}. Poné un nombre en el Inspector.";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedEntity))]
    private void DuplicateEntity()
    {
        if (SelectedEntity is null)
        {
            return;
        }

        var copy = ContentCloner.Clone(SelectedEntity);
        copy.Id = NextId(ContentEntityCatalog.DiscriminatorFor(copy.GetType()));
        copy.Name = string.IsNullOrWhiteSpace(SelectedEntity.Name)
            ? "Copia"
            : $"{SelectedEntity.Name} (copia)";

        _undoStack.Execute(new AddEntitiesAction(_database, [copy]));
        RebuildTree();
        SelectEntity(copy.Id);
        StatusMessage = $"Duplicada como {copy.Id}.";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedEntity))]
    private void DeleteEntity()
    {
        var entity = SelectedEntity;
        if (entity is null)
        {
            return;
        }

        var referrers = ReferenceGraph.ReferrersOf(entity.Id, _database);
        if (referrers.Count > 0 && !_dialogs.Confirm(
                $"{referrers.Count} entidad(es) referencian a «{entity.Name}». ¿Eliminar igual?", "Foundry"))
        {
            return;
        }

        SelectedTreeItem = null;
        _undoStack.Execute(new RemoveEntitiesAction(_database, [entity]));
        RebuildTree();
        StatusMessage = $"Eliminada {entity.Id}.";
    }

    [RelayCommand]
    private void ImportCsv()
    {
        if (IsDirty && !_dialogs.Confirm(
                "Hay cambios sin guardar. La importacion se combina con lo actual. ¿Continuar?", "Foundry"))
        {
            return;
        }

        var path = _filePicker.PickOpenFile(_importer.FileFilter);
        if (path is null)
        {
            return;
        }

        try
        {
            var imported = _importer.Import(path);
            var replaced = 0;
            foreach (var entity in imported)
            {
                if (_database.Contains(entity.Id))
                {
                    _database.Remove(entity.Id);
                    replaced++;
                }

                _database.Add(entity);
            }

            _undoStack.Clear();
            RebuildTree();
            IsDirty = true;
            StatusMessage = $"Importadas {imported.Count} entidades ({replaced} reemplazadas). Guardá para confirmar.";
        }
        catch (ContentImportException ex)
        {
            StatusMessage = $"No se pudo importar: {ex.Message}";
            _dialogs.Inform(ex.Message, "Error al importar CSV");
        }

        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Validate()
    {
        var issues = _validator.Validate(_database);
        var errors = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
        var warnings = issues.Count - errors;

        StatusMessage = issues.Count == 0
            ? "Sin problemas de validacion."
            : $"{errors} error(es), {warnings} aviso(s).";
        _dialogs.Inform(
            issues.Count == 0
                ? "No se encontraron problemas."
                : string.Join(Environment.NewLine, issues.Select(issue => "• " + issue)),
            "Validacion");
    }

    private bool CanSave() => _database.Count > 0;

    private bool CanUndo() => _undoStack.CanUndo;

    private bool CanRedo() => _undoStack.CanRedo;

    private bool HasSelectedEntity() => SelectedEntity is not null;

    partial void OnSelectedTreeItemChanged(object? value)
    {
        OnPropertyChanged(nameof(SelectedEntity));
        Inspector.Load(SelectedEntity, _database);
        Assistant.SetSelectedEntity(SelectedEntity);
        UpdatePreview();
        DuplicateEntityCommand.NotifyCanExecuteChanged();
        DeleteEntityCommand.NotifyCanExecuteChanged();
    }

    private EntityId NextId(string discriminator)
    {
        var baseId = $"{discriminator}.nuevo";
        if (!_database.Contains(new EntityId(baseId)))
        {
            return new EntityId(baseId);
        }

        for (var i = 2; ; i++)
        {
            var candidate = new EntityId($"{baseId}-{i}");
            if (!_database.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private void SelectEntity(EntityId id)
    {
        var node = Categories.SelectMany(category => category.Entities)
            .FirstOrDefault(n => n.Entity.Id.Equals(id));
        if (node is not null)
        {
            node.IsSelected = true;
            SelectedTreeItem = node;
        }
    }

    partial void OnCurrentFilePathChanged(string? value) => OnPropertyChanged(nameof(Title));

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Title));

    partial void OnSearchTextChanged(string value) => RebuildTree();

    private void OnAssistantProposalApplied(object? sender, EventArgs e)
    {
        var proposal = Assistant.Proposal;
        if (proposal.Count == 0)
        {
            return;
        }

        _undoStack.Execute(new AddEntitiesAction(_database, proposal));
        RebuildTree();
        StatusMessage = $"Agregadas {proposal.Count} entidad(es) desde el asistente. Guardá para confirmar.";
    }

    private void OnUndoStackChanged(object? sender, EventArgs e)
    {
        if (_undoStack.CanUndo || _undoStack.CanRedo)
        {
            IsDirty = true;
        }

        if (_database.Count != EntityCount)
        {
            RebuildTree(); // una accion cambio la cantidad de entidades (agregar/quitar por undo/redo)
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
        EntityCount = _database.Count;

        var filter = SearchText.Trim();
        var groups = _database.All
            .Where(entity => Matches(entity, filter))
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

        TreeIsEmpty = Categories.Count == 0;
    }

    private static bool Matches(ContentEntity entity, string filter) =>
        filter.Length == 0
        || entity.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
        || entity.Id.Value.Contains(filter, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Una opcion del menu "Nueva entidad": discriminador + etiqueta visible.</summary>
public sealed record EntityTypeOption(string Discriminator, string Label);
