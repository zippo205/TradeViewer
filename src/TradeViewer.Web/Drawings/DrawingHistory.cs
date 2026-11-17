using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Web.Drawings;

/// <summary>One action on a drawing: the record before (null: it did not exist) and after (null: deleted).</summary>
public sealed record DrawingEdit(DrawingRecord? Before, DrawingRecord? After)
{
    public Guid Id => (After ?? Before)!.Id;
}

/// <summary>Undo and redo of the actions on the drawings of one asset, until the window is reloaded (DR2).</summary>
public sealed class DrawingHistory
{
    /// <summary>The oldest actions are forgotten beyond this many.</summary>
    public const int MaxEdits = 200;

    private readonly LinkedList<DrawingEdit> _undo = new LinkedList<DrawingEdit>();
    private readonly Stack<DrawingEdit> _redo = new Stack<DrawingEdit>();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>A new action; what could be redone is gone.</summary>
    public void Add(DrawingEdit edit)
    {
        _undo.AddLast(edit);
        if (_undo.Count > MaxEdits)
        {
            _undo.RemoveFirst();
        }

        _redo.Clear();
    }

    /// <returns>The action to take back, or null when there is none.</returns>
    public DrawingEdit? Undo()
    {
        var last = _undo.Last;
        if (last == null)
        {
            return null;
        }

        _undo.RemoveLast();
        _redo.Push(last.Value);
        return last.Value;
    }

    /// <returns>The action to do again, or null when there is none.</returns>
    public DrawingEdit? Redo()
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        var edit = _redo.Pop();
        _undo.AddLast(edit);
        return edit;
    }
}
