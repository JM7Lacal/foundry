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
}
