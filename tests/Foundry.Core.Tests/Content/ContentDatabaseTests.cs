using FluentAssertions;
using Foundry.Core.Content;

namespace Foundry.Core.Tests.Content;

public class ContentDatabaseTests
{
    [Fact]
    public void Add_then_Find_returns_the_same_entity()
    {
        var db = new ContentDatabase();
        var troop = new Troop { Id = new EntityId("troop.archer"), Name = "Arquero" };

        db.Add(troop);

        db.Find(new EntityId("troop.archer")).Should().BeSameAs(troop);
        db.Count.Should().Be(1);
    }

    [Fact]
    public void Add_rejects_an_entity_without_id()
    {
        var db = new ContentDatabase();

        var act = () => db.Add(new Troop { Name = "sin id" });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_rejects_a_duplicate_id()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("dup"), Name = "a" });

        var act = () => db.Add(new Tower { Id = new EntityId("dup"), Name = "b" });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OfType_filters_by_concrete_type()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("t1"), Name = "t1" });
        db.Add(new Troop { Id = new EntityId("t2"), Name = "t2" });
        db.Add(new Enemy { Id = new EntityId("e1"), Name = "e1" });

        db.OfType<Troop>().Should().HaveCount(2);
        db.OfType<Enemy>().Should().ContainSingle();
    }

    [Fact]
    public void IsBrokenReference_is_true_when_the_target_is_missing()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("troop.a"), Name = "a" });

        db.IsBrokenReference(new EntityId("troop.a")).Should().BeFalse();
        db.IsBrokenReference(new EntityId("troop.missing")).Should().BeTrue();
        db.IsBrokenReference(null).Should().BeFalse();
    }
}
