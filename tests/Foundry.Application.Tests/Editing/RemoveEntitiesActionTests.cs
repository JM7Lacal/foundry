using FluentAssertions;
using Foundry.Application.Editing;
using Foundry.Application.Undo;
using Foundry.Core.Content;

namespace Foundry.Application.Tests.Editing;

public class RemoveEntitiesActionTests
{
    [Fact]
    public void Apply_removes_and_Revert_restores()
    {
        var db = new ContentDatabase();
        var troop = new Troop { Id = new EntityId("troop.x"), Name = "X" };
        db.Add(troop);
        var stack = new UndoStack();

        stack.Execute(new RemoveEntitiesAction(db, [troop]));
        db.Contains(troop.Id).Should().BeFalse();

        stack.Undo();
        db.Contains(troop.Id).Should().BeTrue();
        db.Get(troop.Id).Should().BeSameAs(troop);
    }
}
