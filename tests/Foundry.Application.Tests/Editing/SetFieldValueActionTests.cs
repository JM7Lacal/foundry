using FluentAssertions;
using Foundry.Application.Editing;
using Foundry.Application.Undo;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Application.Tests.Editing;

public class SetFieldValueActionTests
{
    private static EditableField DamageField() =>
        EditableSchema.For(typeof(Troop)).Fields.Single(f => f.PropertyName == nameof(Troop.Damage));

    [Fact]
    public void Apply_sets_the_new_value_and_Revert_restores_the_old_one()
    {
        var troop = new Troop { Id = new EntityId("t"), Name = "x", Damage = 10 };
        var action = new SetFieldValueAction(troop, DamageField(), 55);

        action.Apply();
        troop.Damage.Should().Be(55);

        action.Revert();
        troop.Damage.Should().Be(10);
    }

    [Fact]
    public void Works_as_a_reversible_step_on_the_undo_stack()
    {
        var troop = new Troop { Id = new EntityId("t"), Name = "x", Damage = 10 };
        var stack = new UndoStack();

        stack.Execute(new SetFieldValueAction(troop, DamageField(), 20));
        stack.Execute(new SetFieldValueAction(troop, DamageField(), 30));
        troop.Damage.Should().Be(30);

        stack.Undo();
        troop.Damage.Should().Be(20);

        stack.Undo();
        troop.Damage.Should().Be(10);

        stack.Redo();
        troop.Damage.Should().Be(20);
    }

    [Fact]
    public void Description_names_the_field_and_the_entity()
    {
        var troop = new Troop { Id = new EntityId("t"), Name = "Arquero" };

        new SetFieldValueAction(troop, DamageField(), 1).Description
            .Should().Contain("Daño").And.Contain("Arquero");
    }
}
