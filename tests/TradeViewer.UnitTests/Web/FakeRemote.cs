using TradeViewer.Contracts.Drawings;
using TradeViewer.Web.Drawings;

namespace TradeViewer.UnitTests.Web;

/// <summary>A server for the save queue: records every request and answers as told; can hold answers back.</summary>
internal sealed class FakeRemote : IDrawingRemote
{
    private readonly Queue<SaveOutcome> _answers = new Queue<SaveOutcome>();
    private TaskCompletionSource? _gate;

    /// <summary>Every request in order: "put {id} {text}" or "delete {id}".</summary>
    public List<string> Requests { get; } = new List<string>();

    /// <summary>What the server holds after the saves that worked.</summary>
    public Dictionary<Guid, DrawingRecord> Stored { get; } = new Dictionary<Guid, DrawingRecord>();

    /// <summary>The next answers; Saved when none are queued.</summary>
    public void Answer(params SaveOutcome[] outcomes)
    {
        foreach (var outcome in outcomes)
        {
            _answers.Enqueue(outcome);
        }
    }

    /// <summary>Requests wait until <see cref="Release"/>.</summary>
    public void Hold()
    {
        _gate = new TaskCompletionSource();
    }

    public void Release()
    {
        var gate = _gate;
        _gate = null;
        gate?.SetResult();
    }

    public async Task<SaveOutcome> PutAsync(DrawingRecord drawing)
    {
        Requests.Add("put " + drawing.Text);
        await WaitAsync();
        var outcome = Next();
        if (outcome.Result == SaveResult.Saved)
        {
            Stored[drawing.Id] = drawing;
        }

        return outcome;
    }

    public async Task<SaveOutcome> DeleteAsync(Guid id)
    {
        Requests.Add("delete");
        await WaitAsync();
        var outcome = Next();
        if (outcome.Result == SaveResult.Saved)
        {
            Stored.Remove(id);
        }

        return outcome;
    }

    private Task WaitAsync()
    {
        return _gate?.Task ?? Task.CompletedTask;
    }

    private SaveOutcome Next()
    {
        return _answers.Count > 0 ? _answers.Dequeue() : SaveOutcome.Saved;
    }
}
