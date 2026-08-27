namespace Foundry.Application.Undo;

/// <summary>
/// Una operacion reversible del editor. Toda mutacion de contenido se modela como una de estas
/// y se ejecuta a traves de <see cref="UndoStack"/>, nunca tocando el modelo directamente.
/// </summary>
public interface IUndoableAction
{
    string Description { get; }

    void Apply();

    void Revert();
}

/// <summary>
/// Pila de undo/redo. <see cref="Execute"/> corre la accion y la apila; <see cref="Undo"/> la
/// revierte y la mueve a la pila de redo; un <see cref="Execute"/> nuevo limpia el redo.
/// </summary>
public sealed class UndoStack
{
    private readonly Stack<IUndoableAction> _undo = new();
    private readonly Stack<IUndoableAction> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Se dispara despues de cualquier cambio en la pila (para refrescar comandos/UI).</summary>
    public event EventHandler? Changed;

    public void Execute(IUndoableAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        action.Apply();
        _undo.Push(action);
        _redo.Clear();
        RaiseChanged();
    }

    public void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        var action = _undo.Pop();
        action.Revert();
        _redo.Push(action);
        RaiseChanged();
    }

    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        var action = _redo.Pop();
        action.Apply();
        _undo.Push(action);
        RaiseChanged();
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
