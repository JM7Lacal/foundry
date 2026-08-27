using FluentAssertions;
using Foundry.Core.Content;

namespace Foundry.Core.Tests.Content;

public class ReferenceGraphTests
{
    private static ContentDatabase Chain()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("troop.a"), Name = "A", UpgradesInto = new EntityId("troop.b") });
        db.Add(new Troop { Id = new EntityId("troop.b"), Name = "B", UpgradesInto = new EntityId("troop.c") });
        db.Add(new Troop { Id = new EntityId("troop.c"), Name = "C" });
        return db;
    }

    [Fact]
    public void ReferrersOf_finds_the_entity_that_points_to_the_target()
    {
        var referrers = ReferenceGraph.ReferrersOf(new EntityId("troop.b"), Chain());

        referrers.Should().ContainSingle();
        referrers[0].From.Name.Should().Be("A");
        referrers[0].Field.Should().Be("Mejora a");
    }

    [Fact]
    public void ReferrersOf_is_empty_when_nobody_points_to_the_target()
    {
        ReferenceGraph.ReferrersOf(new EntityId("troop.a"), Chain()).Should().BeEmpty();
    }

    [Fact]
    public void BrokenLinks_reports_references_to_missing_ids()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("troop.a"), Name = "A", UpgradesInto = new EntityId("troop.ghost") });

        var broken = ReferenceGraph.BrokenLinks(db);

        broken.Should().ContainSingle(link => link.Target == new EntityId("troop.ghost"));
    }

    [Fact]
    public void BrokenLinks_is_empty_when_every_reference_resolves()
    {
        ReferenceGraph.BrokenLinks(Chain()).Should().BeEmpty();
    }
}
