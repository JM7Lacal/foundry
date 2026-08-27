using FluentAssertions;
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
        var vm = new MainViewModel(
            repo,
            new StubSerializer(),
            new StubFilePicker(),
            new StubDialogService(),
            undo,
            new ContentValidator(),
            new InspectorViewModel(undo));
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
    }

    private sealed class StubDialogService : IDialogService
    {
        public bool Confirm(string message, string title) => true;

        public void Inform(string message, string title)
        {
        }
    }
}
