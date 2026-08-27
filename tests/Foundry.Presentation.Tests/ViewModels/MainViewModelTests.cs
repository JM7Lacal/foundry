using FluentAssertions;
using Foundry.Application.Content;
using Foundry.Application.Editing;
using Foundry.Core.Content;
using Foundry.Presentation.Services;
using Foundry.Presentation.ViewModels;
using Foundry.Presentation.ViewModels.Inspector;

namespace Foundry.Presentation.Tests.ViewModels;

public class MainViewModelTests
{
    private static MainViewModel Build(ContentDatabase database)
    {
        return new MainViewModel(
            new StubRepository(database),
            new StubFilePicker(),
            new StubSerializer(),
            new InspectorViewModel());
    }

    private static ContentDatabase SampleDatabase()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("troop.archer"), Name = "Arquero", Damage = 10 });
        db.Add(new Enemy { Id = new EntityId("enemy.orc"), Name = "Orco" });
        return db;
    }

    [Fact]
    public async Task LoadFromAsync_populates_the_category_tree()
    {
        var vm = Build(SampleDatabase());

        await vm.LoadFromAsync("ignored.json");

        vm.Categories.Select(c => c.Name).Should().BeEquivalentTo("Enemigos", "Tropas");
        vm.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Selecting_an_entity_node_loads_the_inspector_and_preview()
    {
        var vm = Build(SampleDatabase());
        await vm.LoadFromAsync("ignored.json");
        var node = vm.Categories.SelectMany(c => c.Entities).First(n => n.Entity.Name == "Arquero");

        vm.SelectedTreeItem = node;

        vm.SelectedEntity.Should().BeSameAs(node.Entity);
        vm.Inspector.HasEntity.Should().BeTrue();
        vm.JsonPreview.Should().Be("<json>");
    }

    [Fact]
    public async Task Editing_a_field_marks_unsaved_changes_and_refreshes_the_preview()
    {
        var vm = Build(SampleDatabase());
        await vm.LoadFromAsync("ignored.json");
        var node = vm.Categories.SelectMany(c => c.Entities).First(n => n.Entity.Name == "Arquero");
        vm.SelectedTreeItem = node;

        var damage = (WholeNumberFieldViewModel)vm.Inspector.Groups
            .SelectMany(g => g.Fields).Single(f => f.Label == "Daño");
        damage.Value = 99;

        ((Troop)node.Entity).Damage.Should().Be(99);
        vm.StatusMessage.Should().Contain("sin guardar");
    }

    private sealed class StubRepository : IContentRepository
    {
        private readonly ContentDatabase _database;

        public StubRepository(ContentDatabase database) => _database = database;

        public Task<ContentDatabase> LoadAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(_database);

        public Task SaveAsync(ContentDatabase database, string path, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubFilePicker : IFilePicker
    {
        public string? PickOpenFile(string filter) => null;

        public string? PickSaveFile(string filter, string? suggestedFileName) => null;
    }

    private sealed class StubSerializer : IContentSerializer
    {
        public string SerializeEntity(ContentEntity entity) => "<json>";
    }
}
