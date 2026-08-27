using FluentAssertions;
using Foundry.Application.Editing;
using Foundry.Application.Undo;
using Foundry.Core.Content;
using Foundry.Core.Editing;

namespace Foundry.Application.Tests.Editing;

public class SetFieldValueActionTests
{
    private static EditableField Field(string propertyName) =>
        EditableSchema.For(typeof(Troop)).Fields.Single(f => f.PropertyName == propertyName);

    [Fact]
    public void Apply_sets_the_new_value_and_Revert_restores_the_old_one()
    {
        var troop = new Troop { Id = new EntityId("t"), Name = "x", Damage = 10 };
        var action = new SetFieldValueAction(troop, Field(nameof(Troop.Damage)), 55);

        action.Apply();
        troop.Damage.Should().Be(55);

        action.Revert();
        troop.Damage.Should().Be(10);
    }

    [Fact]
    public void Rapid_consecutive_edits_to_the_same_field_coalesce_into_one_undo_step()
    {
        var troop = new Troop { Id = new EntityId("t"), Name = "x", Damage = 10 };
        var stack = new UndoStack();
        var damage = Field(nameof(Troop.Damage));

        foreach (var value in new[] { 20, 30, 40, 55 })
        {
            stack.Execute(new SetFieldValueAction(troop, damage, value));
        }

        troop.Damage.Should().Be(55);

        stack.Undo();
        troop.Damage.Should().Be(10); // vuelve al valor previo al gesto, no a cada paso

        stack.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void Edits_to_different_fields_stay_separate()
    {
        var troop = new Troop { Id = new EntityId("t"), Name = "x", Damage = 10, Cost = 100 };
        var stack = new UndoStack();

        stack.Execute(new SetFieldValueAction(troop, Field(nameof(Troop.Damage)), 20));
        stack.Execute(new SetFieldValueAction(troop, Field(nameof(Troop.Cost)), 200));

        stack.Undo();
        troop.Cost.Should().Be(100);
        troop.Damage.Should().Be(20);

        stack.Undo();
        troop.Damage.Should().Be(10);
    }

    [Fact]
    public void Edits_separated_by_more_than_the_coalesce_window_stay_separate()
    {
        var troop = new Troop { Id = new EntityId("t"), Name = "x", Damage = 10 };
        var stack = new UndoStack();
        var damage = Field(nameof(Troop.Damage));

        stack.Execute(new SetFieldValueAction(troop, damage, 20));
        Thread.Sleep(800); // supera la ventana de fusion (700 ms)
        stack.Execute(new SetFieldValueAction(troop, damage, 30));

        stack.Undo();
        troop.Damage.Should().Be(20);

        stack.Undo();
        troop.Damage.Should().Be(10);
    }

    [Fact]
    public void Description_names_the_field_and_the_entity()
    {
        var troop = new Troop { Id = new EntityId("t"), Name = "Arquero" };

        new SetFieldValueAction(troop, Field(nameof(Troop.Damage)), 1).Description
            .Should().Contain("Daño").And.Contain("Arquero");
    }
}
