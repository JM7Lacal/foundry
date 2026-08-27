using FluentAssertions;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Core.Tests.Editing;

public class EditableSchemaTests
{
    [Fact]
    public void Includes_the_inherited_Name_field()
    {
        var schema = EditableSchema.For(typeof(Troop));

        schema.Fields.Should().Contain(f => f.PropertyName == nameof(ContentEntity.Name));
    }

    [Fact]
    public void Infers_the_field_kind_from_the_property_type()
    {
        var fields = EditableSchema.For(typeof(Troop)).Fields.ToDictionary(f => f.PropertyName);

        fields[nameof(Troop.Name)].Kind.Should().Be(FieldKind.Text);
        fields[nameof(Troop.Cost)].Kind.Should().Be(FieldKind.WholeNumber);
        fields[nameof(Troop.AttacksPerSecond)].Kind.Should().Be(FieldKind.Number);
        fields[nameof(Troop.DamageType)].Kind.Should().Be(FieldKind.Choice);
        fields[nameof(Troop.Armor)].Kind.Should().Be(FieldKind.Choice);
        fields[nameof(Troop.UpgradesInto)].Kind.Should().Be(FieldKind.Reference);
    }

    [Fact]
    public void Reads_the_range_attribute()
    {
        var cost = EditableSchema.For(typeof(Troop)).Fields.Single(f => f.PropertyName == nameof(Troop.Cost));

        cost.HasRange.Should().BeTrue();
        cost.Minimum.Should().Be(0);
        cost.Maximum.Should().Be(9999);
    }

    [Fact]
    public void Reference_field_exposes_its_target_type()
    {
        var upgrade = EditableSchema.For(typeof(Troop)).Fields.Single(f => f.PropertyName == nameof(Troop.UpgradesInto));

        upgrade.ReferenceTargetType.Should().Be<Troop>();
    }

    [Fact]
    public void Uses_the_attribute_label_and_group()
    {
        var damage = EditableSchema.For(typeof(Troop)).Fields.Single(f => f.PropertyName == nameof(Troop.Damage));

        damage.Label.Should().Be("Daño");
        damage.Group.Should().Be("Combate");
    }

    [Fact]
    public void Is_cached_per_type()
    {
        EditableSchema.For(typeof(Enemy)).Should().BeSameAs(EditableSchema.For(typeof(Enemy)));
    }

    [Fact]
    public void GetValue_and_SetValue_hit_the_underlying_property()
    {
        var troop = new Troop { Id = new EntityId("t"), Name = "x" };
        var field = EditableSchema.For(typeof(Troop)).Fields.Single(f => f.PropertyName == nameof(Troop.Damage));

        field.SetValue(troop, 77);

        field.GetValue(troop).Should().Be(77);
        troop.Damage.Should().Be(77);
    }
}
