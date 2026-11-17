using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Web.Drawings;

/// <summary>
/// Saves drawings in the background (NF3, WS1): the page shows a change at once and the queue sends it. Per
/// drawing one request at a time; several changes in a row send only the last. A save that fails is tried again
/// until it works, and the page shows "not saved" meanwhile, so a lost connection is visible, never silent.
/// What is not confirmed yet stays known (<see cref="Unsent"/>), so reading the drawings again cannot hide it.
/// </summary>
public sealed class DrawingSaveQueue
{
    private static readonly TimeSpan[] Backoff = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) };

    private readonly IDrawingRemote _remote;
    private readonly Func<TimeSpan, Task> _delay;

    /// <summary>The newest operation per drawing that has not been sent yet.</summary>
    private readonly Dictionary<Guid, Operation> _waiting = new Dictionary<Guid, Operation>();

    /// <summary>The newest operation per drawing that the server has not confirmed yet (sent or not).</summary>
    private readonly Dictionary<Guid, Operation> _unsent = new Dictionary<Guid, Operation>();

    private readonly HashSet<Guid> _running = new HashSet<Guid>();
    private readonly Dictionary<Guid, string> _failing = new Dictionary<Guid, string>();
    private readonly Dictionary<Guid, string> _refused = new Dictionary<Guid, string>();
    private TaskCompletionSource? _idle;

    public DrawingSaveQueue(IDrawingRemote remote)
        : this(remote, delay => Task.Delay(delay))
    {
    }

    /// <param name="delay">Waits before a save is tried again; the tests pass their own.</param>
    public DrawingSaveQueue(IDrawingRemote remote, Func<TimeSpan, Task> delay)
    {
        _remote = remote;
        _delay = delay;
    }

    /// <summary>Something changed that the page shows: waiting saves, a failure, a refusal.</summary>
    public event Action? StateChanged;

    /// <summary>True while a change has not been confirmed by the server: leaving the page would lose it.</summary>
    public bool HasUnsaved => _unsent.Count > 0;

    /// <summary>The message to show while saves fail or were refused; null when everything is saved or on its way.</summary>
    public string? Problem
    {
        get
        {
            if (_refused.Count > 0)
            {
                return "Not saved: " + _refused.Values.First();
            }

            return _failing.Count > 0 ? "Not saved: " + _failing.Values.First() + " Trying again." : null;
        }
    }

    /// <summary>The changes the server has not confirmed: per drawing its newest record, or null for a delete.</summary>
    public IReadOnlyDictionary<Guid, DrawingRecord?> Unsent()
    {
        return _unsent.ToDictionary(pair => pair.Key, pair => pair.Value.Drawing);
    }

    public void Put(DrawingRecord drawing)
    {
        Enqueue(new Operation(drawing.Id, drawing));
    }

    public void Delete(Guid id)
    {
        Enqueue(new Operation(id, null));
    }

    /// <summary>Completes when nothing is being sent or waits to be sent; for the tests.</summary>
    public Task WhenIdleAsync()
    {
        if (_running.Count == 0)
        {
            return Task.CompletedTask;
        }

        _idle ??= new TaskCompletionSource();
        return _idle.Task;
    }

    private void Enqueue(Operation operation)
    {
        _waiting[operation.Id] = operation;
        _unsent[operation.Id] = operation;
        _refused.Remove(operation.Id);
        StateChanged?.Invoke();

        if (!_running.Contains(operation.Id))
        {
            _ = RunAsync(operation.Id);
        }
    }

    private async Task RunAsync(Guid id)
    {
        _running.Add(id);
        var attempt = 0;
        try
        {
            while (_waiting.Remove(id, out var operation))
            {
                var outcome = await SendAsync(operation);
                if (outcome.Result == SaveResult.Retry)
                {
                    _failing[id] = outcome.Message ?? "The server is not reachable.";
                    StateChanged?.Invoke();

                    // Tried again, unless a newer change of the drawing came meanwhile: that one is sent instead.
                    _waiting.TryAdd(id, operation);
                    await _delay(Backoff[Math.Min(attempt++, Backoff.Length - 1)]);
                    continue;
                }

                attempt = 0;
                _failing.Remove(id);
                if (outcome.Result == SaveResult.Refused)
                {
                    _refused[id] = outcome.Message ?? "The server refused the drawing.";
                }

                // Confirmed (or given up) only when nothing newer is waiting.
                if (outcome.Result == SaveResult.Saved && _unsent.TryGetValue(id, out var newest) && ReferenceEquals(newest, operation))
                {
                    _unsent.Remove(id);
                }

                StateChanged?.Invoke();
            }
        }
        finally
        {
            _running.Remove(id);
            if (_running.Count == 0 && _idle != null)
            {
                var idle = _idle;
                _idle = null;
                idle.SetResult();
            }

            StateChanged?.Invoke();
        }
    }

    private async Task<SaveOutcome> SendAsync(Operation operation)
    {
        try
        {
            return operation.Drawing == null ? await _remote.DeleteAsync(operation.Id) : await _remote.PutAsync(operation.Drawing);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new SaveOutcome(SaveResult.Retry, "The server is not reachable.");
        }
    }

    /// <summary>A save (Drawing set) or a delete (Drawing null) of one drawing.</summary>
    private sealed record Operation(Guid Id, DrawingRecord? Drawing);
}
