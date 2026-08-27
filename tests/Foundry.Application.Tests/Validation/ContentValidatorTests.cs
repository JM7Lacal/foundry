using FluentAssertions;
using Foundry.Application.Validation;
using Foundry.Core.Content;

namespace Foundry.Application.Tests.Validation;

public class ContentValidatorTests
{
    private readonly ContentValidator _validator = new();

    [Fact]
    public void A_well_formed_database_has_no_issues()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("troop.archer"), Name = "Arquero", Cost = 90, Damage = 12 });
        db.Add(new Enemy { Id = new EntityId("enemy.orc"), Name = "Orco", Health = 100 });

        _validator.Validate(db).Should().BeEmpty();
    }

    [Fact]
    public void Detects_a_value_outside_its_declared_range()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("t"), Name = "x", Damage = 50_000 }); // rango [0, 9999]

        _validator.Validate(db).Should().ContainSingle(issue => issue.Field == "Daño");
    }

    [Fact]
    public void Detects_an_empty_required_field()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("t"), Name = "  " });

        _validator.Validate(db).Should().Contain(issue => issue.Field == "Nombre");
    }

    [Fact]
    public void Detects_a_dangling_reference()
    {
        var db = new ContentDatabase();
        db.Add(new Troop
        {
            Id = new EntityId("troop.militia"),
            Name = "Milicia",
            UpgradesInto = new EntityId("troop.does-not-exist"),
        });

        _validator.Validate(db).Should().Contain(issue => issue.Message.Contains("inexistente"));
    }

    [Fact]
    public void A_resolvable_reference_is_fine()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("troop.a"), Name = "A", UpgradesInto = new EntityId("troop.b") });
        db.Add(new Troop { Id = new EntityId("troop.b"), Name = "B" });

        _validator.Validate(db).Should().BeEmpty();
    }

    [Fact]
    public void Flags_a_base_entity_that_out_stats_its_upgrade()
    {
        var db = new ContentDatabase();
        db.Add(new Troop
        {
            Id = new EntityId("troop.militia"), Name = "Milicia",
            Damage = 40, Health = 200, Cost = 90, UpgradesInto = new EntityId("troop.knight"),
        });
        db.Add(new Troop { Id = new EntityId("troop.knight"), Name = "Caballero", Damage = 30, Health = 300, Cost = 250 });

        var issues = _validator.Validate(db);

        issues.Should().Contain(i => i.EntityName == "Milicia" && i.Field == "Daño" && i.Message.Contains("supera a su mejora"));
        issues.Should().NotContain(i => i.Field == "Vida"); // 200 <= 300, ok
    }

    [Fact]
    public void A_monotonic_upgrade_chain_is_fine()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("t.1"), Name = "T1", Damage = 10, Health = 100, Cost = 50, UpgradesInto = new EntityId("t.2") });
        db.Add(new Troop { Id = new EntityId("t.2"), Name = "T2", Damage = 20, Health = 150, Cost = 120, UpgradesInto = new EntityId("t.3") });
        db.Add(new Troop { Id = new EntityId("t.3"), Name = "T3", Damage = 35, Health = 220, Cost = 260 });

        _validator.Validate(db).Should().BeEmpty();
    }

    [Fact]
    public void Detects_a_circular_upgrade_chain()
    {
        var db = new ContentDatabase();
        db.Add(new Troop { Id = new EntityId("a"), Name = "A", UpgradesInto = new EntityId("b") });
        db.Add(new Troop { Id = new EntityId("b"), Name = "B", UpgradesInto = new EntityId("a") });

        _validator.Validate(db).Should().Contain(i => i.Message.Contains("circular"));
    }
}
