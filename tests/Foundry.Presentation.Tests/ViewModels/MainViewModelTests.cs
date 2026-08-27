using FluentAssertions;
using Foundry.Application.Ai;
using Foundry.Application.Content;
using Foundry.Application.Editing;
using Foundry.Application.Undo;
using Foundry.Application.Validation;
using Foundry.Core.Content;
using Foundry.Presentation.Services;
using Foundry.Presentation.ViewModels;
using Foundry.Presentation.ViewModels.Inspector;

namespace Foundry.Presentation.Tests.ViewModels;

public class MainViewModelTests
{
    private static (MainViewModel Vm, RecordingRepository Repo) Build(ContentDatabase database)
    {
        var undo = new UndoStack();
        var repo = new RecordingRepository(database);
        var serializer = new StubSerializer();
        var vm = new MainViewModel(
            repo,
            serializer,
            new StubImporter(),
            new StubFilePicker(),
            new StubDialogService(),
            new StubRecentFiles(),
            new StubThemeService(),
            undo,
            new ContentValidator(),
            new InspectorViewModel(undo),
            new AssistantViewModel(new ContentAssistant(new FakeChat(), serializer)));
        return (vm, repo);
    }

    private static ContentDatabase SampleDatabase()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("troop.archer"), Name = "Arquero", Damage = 10 });
        db.Add(new Enemy { Id = new EntityId("enemy.orc"), Name = "Orco" });
        return db;
    }

    private static WholeNumberFieldViewModel DamageField(MainViewModel vm) =>
        (WholeNumberFieldViewModel)vm.Inspector.Groups.SelectMany(g => g.Fields).Single(f => f.Label == "Daño");

    private static async Task<MainViewModel> LoadedWithArcherSelected(ContentDatabase db)
    {
        var (vm, _) = Build(db);
        await vm.LoadFromAsync("ignored.json");
        vm.SelectedTreeItem = vm.Categories.SelectMany(c => c.Entities).First(n => n.Entity.Name == "Arquero");
        return vm;
    }

    [Fact]
    public async Task LoadFromAsync_populates_the_tree_and_is_not_dirty()
    {
        var (vm, _) = Build(SampleDatabase());

        await vm.LoadFromAsync("ignored.json");

        vm.Categories.Select(c => c.Name).Should().BeEquivalentTo("Enemigos", "Tropas");
        vm.SaveCommand.CanExecute(null).Should().BeTrue();
        vm.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task Selecting_an_entity_loads_the_inspector_and_preview()
    {
        var vm = await LoadedWithArcherSelected(SampleDatabase());

        vm.Inspector.HasEntity.Should().BeTrue();
        vm.JsonPreview.Should().Be("<json>");
    }

    [Fact]
    public async Task Editing_marks_dirty_and_undo_reverts_it()
    {
        var vm = await LoadedWithArcherSelected(SampleDatabase());

        DamageField(vm).Value = 99;

        vm.IsDirty.Should().BeTrue();
        vm.Title.Should().EndWith("*");
        vm.UndoCommand.CanExecute(null).Should().BeTrue();

        vm.UndoCommand.Execute(null);
        DamageField(vm).Value.Should().Be(10);
    }

    [Fact]
    public async Task Save_is_blocked_when_the_database_has_validation_issues()
    {
        var db = SampleDatabase();
        var vm = await LoadedWithArcherSelected(db);
        DamageField(vm).Value = 999999; // fuera de rango

        vm.SaveCommand.Execute(null);
        await Task.Yield();

        vm.StatusMessage.Should().Contain("validacion");
    }

    [Fact]
    public async Task New_entity_adds_it_to_the_tree_selected_and_undoable()
    {
        var vm = await LoadedWithArcherSelected(SampleDatabase());
        var before = vm.Categories.SelectMany(c => c.Entities).Count();

        vm.NewEntityCommand.Execute("tower");

        vm.Categories.SelectMany(c => c.Entities).Should().HaveCount(before + 1);
        vm.SelectedEntity.Should().BeOfType<Tower>();
        vm.IsDirty.Should().BeTrue();

        vm.UndoCommand.Execute(null);
        vm.Categories.SelectMany(c => c.Entities).Should().HaveCount(before);
    }

    [Fact]
    public async Task Duplicate_entity_copies_the_stats_with_a_new_id()
    {
        var vm = await LoadedWithArcherSelected(SampleDatabase());
        DamageField(vm).Value = 33;

        vm.DuplicateEntityCommand.Execute(null);

        var copy = (Troop)vm.SelectedEntity!;
        copy.Id.Should().NotBe(new EntityId("troop.archer"));
        copy.Damage.Should().Be(33);
        copy.Name.Should().Contain("copia");
    }

    [Fact]
    public async Task Delete_entity_removes_it_and_undo_brings_it_back()
    {
        var vm = await LoadedWithArcherSelected(SampleDatabase());

        vm.DeleteEntityCommand.Execute(null);

        vm.Categories.SelectMany(c => c.Entities).Should().NotContain(n => n.Entity.Name == "Arquero");

        vm.UndoCommand.Execute(null);
        vm.Categories.SelectMany(c => c.Entities).Should().Contain(n => n.Entity.Name == "Arquero");
    }

    [Fact]
    public void New_entity_menu_options_come_from_the_catalog()
    {
        var (vm, _) = Build(SampleDatabase());

        vm.EntityTypes.Select(o => o.Discriminator).Should().BeEquivalentTo("troop", "tower", "enemy");
    }

    [Fact]
    public async Task The_validation_panel_reflects_issues_after_an_edit()
    {
        var vm = await LoadedWithArcherSelected(SampleDatabase());
        vm.ErrorCount.Should().Be(0);

        DamageField(vm).Value = 999999; // fuera de rango -> error

        vm.ErrorCount.Should().Be(1);
        vm.ValidationIssues.Should().Contain(i => i.IsError && i.Text.Contains("Daño"));
        vm.ValidationSummary.Should().Contain("1 error");
    }

    [Fact]
    public async Task Save_opens_the_issues_panel_when_blocked()
    {
        var vm = await LoadedWithArcherSelected(SampleDatabase());
        DamageField(vm).Value = 999999;

        vm.SaveCommand.Execute(null);
        await Task.Yield();

        vm.IssuesPanelOpen.Should().BeTrue();
    }

    [Fact]
    public void ToggleTheme_flips_the_theme_service()
    {
        var (vm, _) = Build(SampleDatabase());
        vm.IsDarkTheme.Should().BeFalse();

        vm.ToggleThemeCommand.Execute(null);

        vm.IsDarkTheme.Should().BeTrue();
    }

    [Fact]
    public async Task Entities_with_a_validation_error_get_an_error_badge_in_the_tree()
    {
        var vm = await LoadedWithArcherSelected(SampleDatabase());
        DamageField(vm).Value = 999999;

        var node = vm.Categories.SelectMany(c => c.Entities).Single(n => n.Entity.Name == "Arquero");
        node.HasIssue.Should().BeTrue();
        node.IsErrorBadge.Should().BeTrue();
        vm.Categories.Single(c => c.Name == "Tropas").HasIssue.Should().BeTrue();
    }

    [Fact]
    public async Task GoToIssue_selects_the_offending_entity()
    {
        var vm = await LoadedWithArcherSelected(SampleDatabase());
        DamageField(vm).Value = 999999;
        vm.SelectedTreeItem = null;

        vm.GoToIssueCommand.Execute(vm.ValidationIssues.First());

        vm.SelectedEntity!.Id.Should().Be(new EntityId("troop.archer"));
    }

    [Fact]
    public async Task Opening_a_file_adds_it_to_recents()
    {
        var (vm, _) = Build(SampleDatabase());

        await vm.LoadFromAsync("game-a.json");

        vm.RecentFiles.Should().Contain("game-a.json");
        vm.HasRecentFiles.Should().BeTrue();
    }

    [Fact]
    public async Task SearchText_filters_the_tree()
    {
        var (vm, _) = Build(SampleDatabase());
        await vm.LoadFromAsync("ignored.json");

        vm.SearchText = "orc";

        vm.Categories.Should().ContainSingle();
        vm.Categories[0].Name.Should().Be("Enemigos");
        vm.Categories[0].Entities.Should().ContainSingle(n => n.Entity.Name == "Orco");
    }

    [Fact]
    public async Task Save_clears_the_dirty_flag_when_content_is_valid()
    {
        var db = SampleDatabase();
        var (vm, repo) = Build(db);
        await vm.LoadFromAsync("ignored.json");
        vm.SelectedTreeItem = vm.Categories.SelectMany(c => c.Entities).First(n => n.Entity.Name == "Arquero");
        DamageField(vm).Value = 25;

        vm.SaveCommand.Execute(null);
        await Task.Yield();

        repo.Saved.Should().BeTrue();
        vm.IsDirty.Should().BeFalse();
    }

    private sealed class RecordingRepository : IContentRepository
    {
        private readonly ContentDatabase _database;

        public RecordingRepository(ContentDatabase database) => _database = database;

        public bool Saved { get; private set; }

        public Task<ContentDatabase> LoadAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(_database);

        public Task SaveAsync(ContentDatabase database, string path, CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class StubFilePicker : IFilePicker
    {
        public string? PickOpenFile(string filter) => null;

        public string? PickSaveFile(string filter, string? suggestedFileName) => "out.json";
    }

    private sealed class StubSerializer : IContentSerializer
    {
        public string SerializeEntity(ContentEntity entity) => "<json>";

        public IReadOnlyList<ContentEntity> DeserializeEntities(string json) => Array.Empty<ContentEntity>();
    }

    private sealed class FakeChat : IChatCompletion
    {
        public string Name => "fake";

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult("""{ "answer": "ok" }""");
    }

    private sealed class StubImporter : IContentImporter
    {
        public string FormatName => "CSV";

        public string FileFilter => "*.csv";

        public IReadOnlyList<ContentEntity> Import(string path) => Array.Empty<ContentEntity>();
    }

    private sealed class StubDialogService : IDialogService
    {
        public bool Confirm(string message, string title) => true;

        public void Inform(string message, string title)
        {
        }
    }

    private sealed class StubRecentFiles : IRecentFiles
    {
        private readonly List<string> _items = [];

        public IReadOnlyList<string> All => _items;

        public void Add(string path)
        {
            _items.Remove(path);
            _items.Insert(0, path);
        }
    }

    private sealed class StubThemeService : IThemeService
    {
        public bool IsDark { get; private set; }

        public void Toggle() => IsDark = !IsDark;
    }
}
