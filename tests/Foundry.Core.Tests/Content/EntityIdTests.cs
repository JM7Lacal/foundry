using FluentAssertions;
using Foundry.Core.Content;

namespace Foundry.Core.Tests.Content;

public class EntityIdTests
{
    [Fact]
    public void Trims_surrounding_whitespace()
    {
        var id = new EntityId("  troop.archer  ");

        id.Value.Should().Be("troop.archer");
    }

    [Fact]
    public void Equality_is_by_value()
    {
        new EntityId("tower.ballista").Should().Be(new EntityId("tower.ballista"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_empty_values(string? value)
    {
        var act = () => new EntityId(value!);

        act.Should().Throw<ArgumentException>();
    }
}
