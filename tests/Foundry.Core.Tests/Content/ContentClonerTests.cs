using FluentAssertions;
using Foundry.Core.Content;

namespace Foundry.Core.Tests.Content;

public class ContentClonerTests
{
    [Fact]
    public void Copies_every_editable_field()
    {
        var original = new Troop
        {
            Id = new EntityId("troop.a"),
            Name = "Original",
            Cost = 120,
            Damage = 25,
            DamageType = DamageType.Siege,
            Armor = ArmorClass.Heavy,
            UpgradesInto = new EntityId("troop.b"),
        };

        var clone = (Troop)ContentCloner.Clone(original);

        clone.Should().NotBeSameAs(original);
        clone.Name.Should().Be("Original");
        clone.Cost.Should().Be(120);
        clone.Damage.Should().Be(25);
        clone.DamageType.Should().Be(DamageType.Siege);
        clone.Armor.Should().Be(ArmorClass.Heavy);
        clone.UpgradesInto.Should().Be(new EntityId("troop.b"));
    }
}
