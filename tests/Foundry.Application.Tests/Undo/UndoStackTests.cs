using FluentAssertions;
using Foundry.Application.Undo;

namespace Foundry.Application.Tests.Undo;

public class UndoStackTests
{
    [Fact]
    public void Execute_runs_the_action_and_enables_undo()
    {
        var stack = new UndoStack();
        var value = 0;

        stack.Execute(new SetValue(0, 42, v => value = v));

        value.Should().Be(42);
        stack.CanUndo.Should().BeTrue();
        stack.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Undo_then_redo_round_trips_the_value()
    {
        var stack = new UndoStack();
        var value = 0;
        stack.Execute(new SetValue(0, 42, v => value = v));

        stack.Undo();
        value.Should().Be(0);

        stack.Redo();
        value.Should().Be(42);
    }

    [Fact]
    public void Executing_a_new_action_clears_the_redo_stack()
    {
        var stack = new UndoStack();
        var value = 0;
        stack.Execute(new SetValue(0, 1, v => value = v));
        stack.Undo();

        stack.Execute(new SetValue(0, 2, v => value = v));

        stack.CanRedo.Should().BeFalse();
        value.Should().Be(2);
    }

    [Fact]
    public void Raises_Changed_on_execute()
    {
        var stack = new UndoStack();
        var raised = 0;
        stack.Changed += (_, _) => raised++;

        stack.Execute(new SetValue(0, 1, _ => { }));

        raised.Should().Be(1);
    }

    private sealed class SetValue : IUndoableAction
    {
        private readonly Action<int> _set;
        private readonly int _newValue;
        private readonly int _oldValue;

        public SetValue(int oldValue, int newValue, Action<int> set)
        {
            _oldValue = oldValue;
            _newValue = newValue;
            _set = set;
        }

        public string Description => $"Set {_oldValue} -> {_newValue}";

        public void Apply() => _set(_newValue);

        public void Revert() => _set(_oldValue);
    }
}
