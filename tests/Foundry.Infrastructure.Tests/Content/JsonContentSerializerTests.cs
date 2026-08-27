using FluentAssertions;
using Foundry.Application.Content;
using Foundry.Core.Content;
using Foundry.Infrastructure.Content;

namespace Foundry.Infrastructure.Tests.Content;

public class JsonContentSerializerTests
{
    private readonly JsonContentSerializer _serializer = new();

    [Fact]
    public void Serialize_then_deserialize_round_trips_an_entity()
    {
        var troop = new Troop { Id = new EntityId("troop.x"), Name = "X", Damage = 21, DamageType = DamageType.Magic };

        var json = _serializer.SerializeEntity(troop);
        var back = _serializer.DeserializeEntities(json).OfType<Troop>().Single();

        back.Id.Should().Be(troop.Id);
        back.Damage.Should().Be(21);
        back.DamageType.Should().Be(DamageType.Magic);
    }

    [Fact]
    public void DeserializeEntities_accepts_an_entities_wrapper()
    {
        var list = _serializer.DeserializeEntities(
            """{ "entities": [ { "$type": "troop", "id": "t.1", "name": "A" }, { "$type": "enemy", "id": "e.1", "name": "B" } ] }""");

        list.Should().HaveCount(2);
        list.OfType<Enemy>().Single().Name.Should().Be("B");
    }

    [Fact]
    public void DeserializeEntities_accepts_a_bare_array()
    {
        _serializer.DeserializeEntities("""[ { "$type": "tower", "id": "tw.1", "name": "T" } ]""")
            .Should().ContainSingle(e => e is Tower);
    }

    [Fact]
    public void DeserializeEntities_accepts_pascal_case_field_names_from_a_model()
    {
        var troop = _serializer.DeserializeEntities(
            """{ "$type": "troop", "Id": "troop.x", "Name": "X", "Cost": 150, "DamageType": "siege" }""")
            .OfType<Troop>().Single();

        troop.Cost.Should().Be(150);
        troop.DamageType.Should().Be(DamageType.Siege);
    }

    [Fact]
    public void DeserializeEntities_rejects_an_unknown_type()
    {
        var act = () => _serializer.DeserializeEntities("""{ "$type": "dragon", "id": "d.1", "name": "D" }""");

        act.Should().Throw<ContentRepositoryException>();
    }
}
