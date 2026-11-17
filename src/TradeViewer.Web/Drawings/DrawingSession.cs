using System.Text.Json;
using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Web.Drawings;

/// <summary>
/// The drawings of the asset shown (Web owns the list, the chart module draws it): what the user does with them
/// shows at once, goes into the undo history of the asset and into the save queue. A locked drawing cannot be
/// changed or deleted until it is unlocked (DR2). One per window: histories last until the window is reloaded.
/// </summary>
public sealed class DrawingSession
{
    private readonly DrawingSaveQueue _saves;
    private readonly Dictionary<string, DrawingHistory> _histories = new Dictionary<string, DrawingHistory>();
    private List<DrawingRecord> _drawings = new List<DrawingRecord>();

    public DrawingSession(DrawingSaveQueue saves)
    {
        _saves = saves;
    }

    /// <summary>The list or the selection changed: the page gives the chart the drawings again and redraws.</summary>
    public event Action? Changed;

    public string Source { get; private set; } = string.Empty;

    public string Symbol { get; private set; } = string.Empty;

    public IReadOnlyList<DrawingRecord> Drawings => _drawings;

    public Guid? SelectedId { get; private set; }

    public DrawingRecord? Selected => SelectedId == null ? null : Find(SelectedId.Value);

    public bool CanUndo => History.CanUndo;

    public bool CanRedo => History.CanRedo;

    private DrawingHistory History
    {
        get
        {
            var key = Source + "/" + Symbol;
            if (!_histories.TryGetValue(key, out var history))
            {
                history = new DrawingHistory();
                _histories[key] = history;
            }

            return history;
        }
    }

    /// <summary>
    /// Shows the drawings of an asset as the server sent them. Changes the server has not confirmed yet are laid
    /// over them, so reading again never hides what the user did (priority 1).
    /// </summary>
    public void Show(string source, string symbol, IReadOnlyList<DrawingRecord> stored)
    {
        Source = source;
        Symbol = symbol;

        var drawings = stored.Where(drawing => drawing.Source == source && drawing.Symbol == symbol).ToList();
        foreach (var pair in _saves.Unsent())
        {
            var index = drawings.FindIndex(drawing => drawing.Id == pair.Key);
            if (pair.Value == null)
            {
                if (index >= 0)
                {
                    drawings.RemoveAt(index);
                }
            }
            else if (pair.Value.Source == source && pair.Value.Symbol == symbol)
            {
                if (index >= 0)
                {
                    drawings[index] = pair.Value;
                }
                else
                {
                    drawings.Add(pair.Value);
                }
            }
        }

        _drawings = drawings;
        if (SelectedId != null && Find(SelectedId.Value) == null)
        {
            SelectedId = null;
        }

        Changed?.Invoke();
    }

    public void Select(Guid? id)
    {
        var selected = id != null && Find(id.Value) != null ? id : null;
        if (selected != SelectedId)
        {
            SelectedId = selected;
            Changed?.Invoke();
        }
    }

    /// <summary>The chart made a new drawing.</summary>
    public void Created(DrawingRecord drawing)
    {
        if (drawing.Source != Source || drawing.Symbol != Symbol || Find(drawing.Id) != null)
        {
            return;
        }

        Do(new DrawingEdit(null, drawing));
    }

    /// <summary>The chart moved or reshaped a drawing; the chart refuses that for a locked one already.</summary>
    public void Moved(DrawingRecord drawing)
    {
        var before = Find(drawing.Id);
        if (before != null && !before.Locked)
        {
            Do(new DrawingEdit(before, drawing));
        }
    }

    /// <summary>A change from the bar of the selected drawing (colour, width, line style, text, lock).</summary>
    /// <returns>False when the drawing is locked and the change is more than unlocking it.</returns>
    public bool Edit(DrawingRecord drawing)
    {
        var before = Find(drawing.Id);
        if (before == null || (before.Locked && !OnlyLockDiffers(before, drawing)))
        {
            return false;
        }

        if (Same(before, drawing))
        {
            return true;
        }

        Do(new DrawingEdit(before, drawing));
        return true;
    }

    /// <returns>False when there is no such drawing or it is locked.</returns>
    public bool Delete(Guid id)
    {
        var before = Find(id);
        if (before == null || before.Locked)
        {
            return false;
        }

        Do(new DrawingEdit(before, null));
        return true;
    }

    public bool Undo()
    {
        var edit = History.Undo();
        if (edit == null)
        {
            return false;
        }

        Apply(edit.After, edit.Before, edit.Id);
        return true;
    }

    public bool Redo()
    {
        var edit = History.Redo();
        if (edit == null)
        {
            return false;
        }

        Apply(edit.Before, edit.After, edit.Id);
        return true;
    }

    private void Do(DrawingEdit edit)
    {
        History.Add(edit);
        Apply(edit.Before, edit.After, edit.Id);
    }

    /// <summary>Puts the drawing into the state `to` (null: gone), shows it and saves it.</summary>
    private void Apply(DrawingRecord? from, DrawingRecord? to, Guid id)
    {
        var index = _drawings.FindIndex(drawing => drawing.Id == id);
        if (to == null)
        {
            if (index >= 0)
            {
                _drawings.RemoveAt(index);
            }

            if (SelectedId == id)
            {
                SelectedId = null;
            }

            _saves.Delete(id);
        }
        else
        {
            if (index >= 0)
            {
                _drawings[index] = to;
            }
            else
            {
                _drawings.Add(to); // a drawing that comes back (undo of a delete) goes on top, as the server keeps it
            }

            _saves.Put(to);
        }

        Changed?.Invoke();
    }

    private DrawingRecord? Find(Guid id)
    {
        return _drawings.FirstOrDefault(drawing => drawing.Id == id);
    }

    private static bool OnlyLockDiffers(DrawingRecord before, DrawingRecord after)
    {
        return Same(before with { Locked = after.Locked }, after);
    }

    /// <summary>Records holding lists compare those by reference; their JSON compares the content.</summary>
    private static bool Same(DrawingRecord a, DrawingRecord b)
    {
        return JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
    }
}
