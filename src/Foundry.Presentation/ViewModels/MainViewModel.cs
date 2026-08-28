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
    private readonly IRecentFiles _recentFiles;
    private readonly IThemeService _theme;
    private readonly UndoStack _undoStack;
    private readonly ContentValidator _validator;

    private ContentDatabase _database = new();
    private readonly Dictionary<EntityId, NodeBadge> _badgeByEntity = [];
    private readonly Dictionary<EntityId, string> _issueTextByEntity = [];

    /// <summary>Profundidad del <see cref="UndoStack"/> en el ultimo guardado (o carga). El
    /// documento esta "sucio" mientras la profundidad actual difiera de este valor; asi, deshacer
    /// hasta el punto guardado vuelve a marcar "limpio".</summary>
    private int _savedUndoDepth;

    [ObservableProperty]
    private int _entityCount;

    [ObservableProperty]
    private bool _treeIsEmpty = true;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private bool _issuesPanelOpen;

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
        IRecentFiles recentFiles,
        IThemeService theme,
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
        _recentFiles = recentFiles;
        _theme = theme;
        _undoStack = undoStack;
        _validator = validator;
        Inspector = inspector;
        Assistant = assistant;

        RefreshRecent();
        _undoStack.Changed += OnUndoStackChanged;
        Assistant.ProposalApplied += OnAssistantProposalApplied;
    }

    public ObservableCollection<ContentCategoryViewModel> Categories { get; } = [];

    public InspectorViewModel Inspector { get; }

    public AssistantViewModel Assistant { get; }

    public ObservableCollection<ValidationIssueViewModel> ValidationIssues { get; } = [];

    public ObservableCollection<string> RecentFiles { get; } = [];

    public bool HasRecentFiles => RecentFiles.Count > 0;

    public string ValidationSummary => ErrorCount == 0 && WarningCount == 0
        ? "Validacion: sin problemas"
        : $"Validacion: {ErrorCount} error(es) · {WarningCount} aviso(s)";

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

    /// <summary>Abre un archivo del usuario (queda como archivo actual y en "Recientes").</summary>
    public Task LoadFromAsync(string path) => LoadContentAsync(path, asFile: true);

    /// <summary>Carga el ejemplo empaquetado como documento "sin titulo": el primer guardado
    /// siempre pide destino, para no escribir dentro de <c>bin/</c> (que se regenera en cada build).</summary>
    public Task LoadSampleAsync(string path) => LoadContentAsync(path, asFile: false);

    private async Task LoadContentAsync(string path, bool asFile)
    {
        StatusMessage = "Cargando...";

        try
        {
            _database = await _repository.LoadAsync(path).ConfigureAwait(true);
            _undoStack.Clear();
            _savedUndoDepth = 0;
            CurrentFilePath = asFile ? path : null;
            RebuildTree();
            RefreshValidation();
            Assistant.SetContext(_database);
            IsDirty = false;
            StatusMessage = asFile
                ? $"{_database.Count} entidades — {Path.GetFileName(path)}"
                : $"{_database.Count} entidades (ejemplo). Al guardar te pido dónde.";

            if (asFile)
            {
                _recentFiles.Add(path);
                RefreshRecent();
            }
        }
        catch (ContentRepositoryException ex)
        {
            StatusMessage = $"No se pudo cargar: {ex.Message}";
        }

        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task OpenRecent(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (IsDirty && !_dialogs.Confirm(
                "Hay cambios sin guardar. ¿Descartarlos y abrir otro archivo?", "Foundry"))
        {
            return;
        }

        await LoadFromAsync(path).ConfigureAwait(true);
    }

    private void RefreshRecent()
    {
        RecentFiles.Clear();
        foreach (var path in _recentFiles.All)
        {
            RecentFiles.Add(path);
        }

        OnPropertyChanged(nameof(HasRecentFiles));
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

    /// <summary>
    /// Guarda todo el contenido a disco (Ctrl+S). No hay gate de validacion: el editor deja
    /// guardar trabajo en progreso (los estados intermedios invalidos son normales al editar);
    /// el panel muestra los avisos y el chequeo duro corre al cerrar. Si el archivo actual es el
    /// ejemplo (dentro del directorio del <c>.exe</c>) o no hay archivo, pide "Guardar como".
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (_database.Count == 0)
        {
            return;
        }

        var path = CurrentFilePath is { } current && !IsUnderAppDirectory(current)
            ? current
            : _filePicker.PickSaveFile("Contenido de juego (*.json)|*.json", "contenido.json");
        if (path is null)
        {
            return;
        }

        try
        {
            await _repository.SaveAsync(_database, path).ConfigureAwait(true);
            CurrentFilePath = path;
            _recentFiles.Add(path);
            RefreshRecent();
            _savedUndoDepth = _undoStack.UndoDepth;
            IsDirty = false;
            StatusMessage = $"Guardado · {DateTime.Now:HH:mm:ss} — {Path.GetFileName(path)}";
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
            _savedUndoDepth = -1; // sin historial pero con cambios: fuerza "sucio" hasta guardar
            RebuildTree();
            RefreshValidation();
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
        RefreshValidation();
        IssuesPanelOpen = true;
        StatusMessage = ValidationSummary;
    }

    public bool IsDarkTheme => _theme.IsDark;

    [RelayCommand]
    private void ToggleTheme()
    {
        _theme.Toggle();
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    [RelayCommand]
    private void GoToIssue(ValidationIssueViewModel? issue)
    {
        if (issue is null)
        {
            return;
        }

        SearchText = string.Empty;
        SelectEntity(issue.EntityId);
    }

    private void RefreshValidation()
    {
        ValidationIssues.Clear();

        var issues = _validator.Validate(_database)
            .OrderBy(issue => issue.Severity)
            .ToList();

        foreach (var issue in issues)
        {
            ValidationIssues.Add(new ValidationIssueViewModel(issue));
        }

        ErrorCount = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
        WarningCount = issues.Count - ErrorCount;
        OnPropertyChanged(nameof(ValidationSummary));

        _badgeByEntity.Clear();
        _issueTextByEntity.Clear();
        foreach (var issue in issues)
        {
            var badge = issue.Severity == ValidationSeverity.Error ? NodeBadge.Error : NodeBadge.Warning;
            if (badge > _badgeByEntity.GetValueOrDefault(issue.EntityId))
            {
                _badgeByEntity[issue.EntityId] = badge;
            }

            var line = $"{(issue.Severity == ValidationSeverity.Error ? "Error" : "Aviso")} · {issue.Field}: {issue.Message}";
            _issueTextByEntity[issue.EntityId] = _issueTextByEntity.TryGetValue(issue.EntityId, out var prev)
                ? $"{prev}\n{line}"
                : line;
        }

        ApplyBadges();
    }

    private void ApplyBadges()
    {
        foreach (var category in Categories)
        {
            var worst = NodeBadge.None;
            var count = 0;
            foreach (var node in category.Entities)
            {
                node.Badge = _badgeByEntity.GetValueOrDefault(node.Entity.Id);
                node.BadgeTooltip = _issueTextByEntity.GetValueOrDefault(node.Entity.Id);
                if (node.Badge > worst)
                {
                    worst = node.Badge;
                }

                if (node.Badge != NodeBadge.None)
                {
                    count++;
                }
            }

            category.Badge = worst;
            category.BadgeTooltip = count == 0 ? null : $"{count} entidad(es) con problemas de validación";
        }
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
        var proposal = Assistant.Proposal.ToList();
        if (proposal.Count == 0)
        {
            return;
        }

        // Igual que crear una entidad a mano: entra a la base via undo stack y se persiste con
        // Ctrl+S como todo lo demas. Se selecciona la nueva para que se vea.
        _undoStack.Execute(new AddEntitiesAction(_database, proposal));
        RebuildTree();
        RefreshValidation();
        SelectEntity(proposal[^1].Id);
        StatusMessage = proposal.Count == 1
            ? $"Agregada «{proposal[0].Name}» (propuesta de la IA). Guardá con Ctrl+S."
            : $"Agregadas {proposal.Count} entidades de la IA. Guardá con Ctrl+S.";
    }

    private static bool IsUnderAppDirectory(string path)
    {
        try
        {
            return Path.GetFullPath(path).StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private void OnUndoStackChanged(object? sender, EventArgs e)
    {
        // Sucio = la pila difiere del punto guardado (deshacer hasta ahi vuelve a "limpio").
        IsDirty = _undoStack.UndoDepth != _savedUndoDepth;

        if (_database.Count != EntityCount)
        {
            RebuildTree(); // una accion cambio la cantidad de entidades (agregar/quitar por undo/redo)
        }

        Inspector.RefreshValues();
        UpdatePreview();
        RefreshValidation();
        (SelectedTreeItem as EntityNodeViewModel)?.Refresh();

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();

        StatusMessage = !IsDirty
            ? "Sin cambios sin guardar."
            : Inspector.HasErrors
                ? "Cambios sin guardar — hay errores de validacion."
                : "Cambios sin guardar.";
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
        ApplyBadges();
    }

    private static bool Matches(ContentEntity entity, string filter) =>
        filter.Length == 0
        || entity.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
        || entity.Id.Value.Contains(filter, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Una opcion del menu "Nueva entidad": discriminador + etiqueta visible.</summary>
public sealed record EntityTypeOption(string Discriminator, string Label);
