namespace FloorPlanner.Core.Editing;

/// <summary>Snapshot-based undo/redo over an immutable document type.</summary>
public sealed class History<T>(T initial)
{
    private readonly Stack<T> _undo = new();
    private readonly Stack<T> _redo = new();

    public T Current { get; private set; } = initial;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Push(T next)
    {
        _undo.Push(Current);
        _redo.Clear();
        Current = next;
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redo.Push(Current);
        Current = _undo.Pop();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undo.Push(Current);
        Current = _redo.Pop();
    }

    /// <summary>Replaces the document and forgets all history (e.g. after loading a file).</summary>
    public void Reset(T document)
    {
        _undo.Clear();
        _redo.Clear();
        Current = document;
    }
}
