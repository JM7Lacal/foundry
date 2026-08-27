using FluentAssertions;
using Foundry.Application.Content;
using Foundry.Core.Content;
using Foundry.Infrastructure.Content;

namespace Foundry.Infrastructure.Tests.Content;

public sealed class JsonContentRepositoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"foundry-{Guid.NewGuid():N}.json");
    private readonly JsonContentRepository _repository = new();

    [Fact]
    public async Task Save_then_Load_round_trips_entities_and_their_values()
    {
        var original = new ContentDatabase();
        original.Add(new Troop
        {
            Id = new EntityId("troop.archer"),
            Name = "Arquero",
            Cost = 90,
            Damage = 12,
            DamageType = DamageType.Siege,
            Armor = ArmorClass.Light,
            UpgradesInto = new EntityId("troop.crossbowman"),
        });
        original.Add(new Enemy { Id = new EntityId("enemy.orc"), Name = "Orco", Health = 160, Kind = EnemyKind.Ground });

        await _repository.SaveAsync(original, _path);
        var reloaded = await _repository.LoadAsync(_path);

        reloaded.Count.Should().Be(2);

        var troop = reloaded.OfType<Troop>().Single();
        troop.Name.Should().Be("Arquero");
        troop.Cost.Should().Be(90);
        troop.DamageType.Should().Be(DamageType.Siege);
        troop.Armor.Should().Be(ArmorClass.Light);
        troop.UpgradesInto.Should().Be(new EntityId("troop.crossbowman"));

        reloaded.OfType<Enemy>().Single().Health.Should().Be(160);
    }

    [Fact]
    public async Task Load_fails_with_a_clear_error_on_an_unknown_entity_type()
    {
        await File.WriteAllTextAsync(
            _path,
            """{ "schemaVersion": 1, "entities": [ { "$type": "spaceship", "id": "x", "name": "x" } ] }""");

        var act = () => _repository.LoadAsync(_path);

        await act.Should().ThrowAsync<ContentRepositoryException>();
    }

    [Fact]
    public async Task Load_fails_when_the_file_does_not_exist()
    {
        var act = () => _repository.LoadAsync(Path.Combine(Path.GetTempPath(), "does-not-exist.json"));

        await act.Should().ThrowAsync<ContentRepositoryException>();
    }

    [Fact]
    public async Task Saved_file_uses_the_type_discriminator_and_string_enums()
    {
        var db = new ContentDatabase();
        db.Add(new Tower { Id = new EntityId("tower.cannon"), Name = "Cañon", DamageType = DamageType.Siege });

        await _repository.SaveAsync(db, _path);
        var json = await File.ReadAllTextAsync(_path);

        json.Should().Contain("\"$type\": \"tower\"");
        json.Should().Contain("\"damageType\": \"siege\"");
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
