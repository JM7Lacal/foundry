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

    /// <summary>
    /// Intenta absorber una accion inmediatamente posterior (misma operacion, en rafaga — p. ej.
    /// arrastrar un deslizador). Si devuelve <c>true</c>, <paramref name="newer"/> no se apila:
    /// esta accion actualiza su "valor nuevo" conservando el "valor viejo" original.
    /// </summary>
    bool TryCoalesceWith(IUndoableAction newer) => false;
}

/// <summary>
/// Pila de undo/redo. <see cref="Execute"/> corre la accion y la apila; <see cref="Undo"/> la
/// revierte y la mueve a la pila de redo; un <see cref="Execute"/> nuevo limpia el redo.
/// Acciones consecutivas del mismo tipo que aceptan fusionarse (<see cref="IUndoableAction.TryCoalesceWith"/>)
/// cuentan como un solo paso de undo.
/// </summary>
public sealed class UndoStack
{
    private readonly Stack<IUndoableAction> _undo = new();
    private readonly Stack<IUndoableAction> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Cantidad de pasos que se pueden deshacer. Sirve para marcar el punto guardado y saber si
    /// el documento esta "sucio" (profundidad actual != profundidad al guardar).
    /// </summary>
    public int UndoDepth => _undo.Count;

    /// <summary>Se dispara despues de cualquier cambio en la pila (para refrescar comandos/UI).</summary>
    public event EventHandler? Changed;

    public void Execute(IUndoableAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        action.Apply();

        if (_undo.Count == 0 || !_undo.Peek().TryCoalesceWith(action))
        {
            _undo.Push(action);
        }

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
