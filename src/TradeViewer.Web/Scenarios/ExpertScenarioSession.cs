using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Web.Scenarios;

/// <summary>
/// The expert scenario in work on the page (SC12-SC14, proposal 10): the newest answer of the server (scenario,
/// shapes, problems, levels), whether the user has changed something (only then it is saved, SC13), and undo and redo
/// of the whole scenario until another one is opened. Key names the scenario in the save queue, also before it has an
/// id. The id and the times of the first save are kept on every state, so undo never brings back a scenario without id.
/// </summary>
public sealed class ExpertScenarioSession
{
    /// <summary>The oldest states are forgotten beyond this many.</summary>
    public const int MaxUndo = 200;

    private readonly LinkedList<ExpertScenarioRecord> _undo = new LinkedList<ExpertScenarioRecord>();
    private readonly Stack<ExpertScenarioRecord> _redo = new Stack<ExpertScenarioRecord>();

    /// <summary>The scenario as last confirmed by the server: its id and times are put on every state.</summary>
    private ExpertScenarioRecord? _saved;

    public ExpertScenarioSession(ExpertScenarioResponse response)
        : this(Guid.NewGuid(), response)
    {
    }

    public ExpertScenarioSession(Guid key, ExpertScenarioResponse response)
    {
        Key = key;
        Response = response;
    }

    public Guid Key { get; }

    public ExpertScenarioResponse Response { get; private set; }

    public ExpertScenarioRecord Scenario => Response.Scenario;

    /// <summary>True once the user has drawn, changed or written something; viewing changes nothing (SC13).</summary>
    public bool Changed { get; private set; }

    /// <summary>True while there is a change the server has not confirmed as saved: Save is offered (SC13).</summary>
    public bool Dirty { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// A first save can only be made once wave 2 end is placed, because the id needs its month (SC14). Until then the
    /// page warns before it is left.
    /// </summary>
    public bool WaitsForWave2End => Scenario.Id == null && Scenario.Waves?.Wave2End == null;

    /// <summary>A change of the user: the state before can be taken back, what could be redone is gone.</summary>
    public void Change(ExpertScenarioResponse after)
    {
        _undo.AddLast(Scenario);
        if (_undo.Count > MaxUndo)
        {
            _undo.RemoveFirst();
        }

        _redo.Clear();
        Response = Keep(after);
        Changed = true;
        Dirty = true;
    }

    /// <summary>The same scenario answered again (e.g. after its asset loaded): new shapes and asset state, no change of the user.</summary>
    public void Refresh(ExpertScenarioResponse response)
    {
        Response = response with { Scenario = Scenario }; // the same state: an unsaved change stays unsaved, a save of it stays its save
    }

    /// <summary>The state undo goes back to, or null; it is only taken when <see cref="Undone"/> follows.</summary>
    public ExpertScenarioRecord? UndoTarget() => _undo.Last == null ? null : Stamp(_undo.Last.Value);

    /// <summary>The state redo goes to, or null; it is only taken when <see cref="Redone"/> follows.</summary>
    public ExpertScenarioRecord? RedoTarget() => _redo.Count == 0 ? null : Stamp(_redo.Peek());

    /// <summary>The server's answer for <see cref="UndoTarget"/>.</summary>
    public void Undone(ExpertScenarioResponse response)
    {
        if (_undo.Last == null)
        {
            return;
        }

        _undo.RemoveLast();
        _redo.Push(Scenario);
        Response = Keep(response);
        Changed = true;
        Dirty = true;
    }

    /// <summary>The server's answer for <see cref="RedoTarget"/>.</summary>
    public void Redone(ExpertScenarioResponse response)
    {
        if (_redo.Count == 0)
        {
            return;
        }

        _redo.Pop();
        _undo.AddLast(Scenario);
        Response = Keep(response);
        Changed = true;
        Dirty = true;
    }

    /// <summary>
    /// A save was confirmed: the id it took and its times are kept; the rest of the scenario stays as the user has it,
    /// because a change may have been made after Save. Sent is the state that was saved: when it is still the one
    /// shown, nothing is left to save.
    /// </summary>
    public void Saved(ExpertScenarioRecord sent, ExpertScenarioRecord saved)
    {
        var unchanged = ReferenceEquals(Scenario, sent);
        Response = Response with { Scenario = Stamp(Scenario, saved) };
        _saved = saved;
        if (unchanged)
        {
            Dirty = false;
        }
    }

    private ExpertScenarioResponse Keep(ExpertScenarioResponse response)
    {
        return response with { Scenario = Stamp(response.Scenario) };
    }

    private ExpertScenarioRecord Stamp(ExpertScenarioRecord scenario)
    {
        return _saved == null ? scenario : Stamp(scenario, _saved);
    }

    private static ExpertScenarioRecord Stamp(ExpertScenarioRecord scenario, ExpertScenarioRecord saved)
    {
        return scenario with
        {
            Id = saved.Id ?? scenario.Id,
            CreatedAt = saved.CreatedAt ?? scenario.CreatedAt,
            UpdatedAt = saved.UpdatedAt ?? scenario.UpdatedAt
        };
    }
}
