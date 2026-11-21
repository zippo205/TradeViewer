using TradeViewer.Contracts.Scenarios;
using TradeViewer.Web.Drawings;

namespace TradeViewer.Web.Scenarios;

/// <summary>What became of one save of a scenario: the scenario as the server stored it, or the message to show.</summary>
public sealed record ScenarioSaveOutcome(SaveResult Result, ExpertScenarioRecord? Saved = null, string? Message = null);

/// <summary>Where the save queue writes scenarios: the Api (ScenariosApi), or a fake in the tests.</summary>
public interface IExpertScenarioRemote
{
    /// <summary>Without an id the first save, which takes the id (SC14); with an id it writes over the file (SC13).</summary>
    Task<ScenarioSaveOutcome> SaveAsync(ExpertScenarioRecord scenario);
}

/// <summary>
/// Saves expert scenarios in the background, as the DrawingSaveQueue saves drawings (SC13, NF3, WS1): the page shows a
/// change at once and the queue sends it. Per scenario one request at a time; several changes in a row send only the
/// last. A save that fails is tried again until it works, and the page shows "not saved" meanwhile. The first save of
/// a new scenario takes its id; every later change of it is sent with that id, also one made while the first save was
/// on its way, so a scenario is never created twice.
/// </summary>
public sealed class ExpertScenarioSaveQueue
{
    private static readonly TimeSpan[] Backoff = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) };

    private readonly IExpertScenarioRemote _remote;
    private readonly Func<TimeSpan, Task> _delay;
    private readonly Dictionary<Guid, ExpertScenarioRecord> _waiting = new Dictionary<Guid, ExpertScenarioRecord>();
    private readonly Dictionary<Guid, ExpertScenarioRecord> _unsent = new Dictionary<Guid, ExpertScenarioRecord>();
    private readonly Dictionary<Guid, string> _ids = new Dictionary<Guid, string>();
    private readonly HashSet<Guid> _running = new HashSet<Guid>();
    private readonly HashSet<Guid> _forgotten = new HashSet<Guid>();
    private readonly Dictionary<Guid, string> _failing = new Dictionary<Guid, string>();
    private readonly Dictionary<Guid, string> _refused = new Dictionary<Guid, string>();
    private readonly Dictionary<Guid, TaskCompletionSource> _stopped = new Dictionary<Guid, TaskCompletionSource>();
    private TaskCompletionSource? _idle;

    public ExpertScenarioSaveQueue(IExpertScenarioRemote remote)
        : this(remote, delay => Task.Delay(delay))
    {
    }

    /// <param name="delay">Waits before a save is tried again; the tests pass their own.</param>
    public ExpertScenarioSaveQueue(IExpertScenarioRemote remote, Func<TimeSpan, Task> delay)
    {
        _remote = remote;
        _delay = delay;
    }

    /// <summary>Something changed that the page shows: waiting saves, a failure, a refusal.</summary>
    public event Action? StateChanged;

    /// <summary>A save was confirmed: the key of the scenario and the scenario as stored (with its id).</summary>
    public event Action<Guid, ExpertScenarioRecord>? Saved;

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

    public bool IsUnsaved(Guid key) => _unsent.ContainsKey(key);

    /// <summary>The id the scenario of this key has, also when the page has not heard of its first save yet.</summary>
    public string? IdOf(Guid key) => _ids.GetValueOrDefault(key);

    /// <summary>The key under which the scenario with this id is saved, so opening it again goes on in the same order of saves.</summary>
    public Guid? KeyOf(string id)
    {
        foreach (var (key, known) in _ids)
        {
            if (known == id)
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>The newest change of the scenario with this id that the server has not confirmed, so reading the file again cannot hide it.</summary>
    public ExpertScenarioRecord? UnsentOf(string id)
    {
        foreach (var (key, scenario) in _unsent)
        {
            if ((_ids.GetValueOrDefault(key) ?? scenario.Id) == id)
            {
                return WithId(key, scenario);
            }
        }

        return null;
    }

    public void Put(Guid key, ExpertScenarioRecord scenario)
    {
        _forgotten.Remove(key);
        if (scenario.Id != null)
        {
            _ids.TryAdd(key, scenario.Id);
        }

        _waiting[key] = scenario;
        _unsent[key] = scenario;
        _refused.Remove(key);
        StateChanged?.Invoke();

        if (!_running.Contains(key))
        {
            _ = RunAsync(key);
        }
    }

    /// <summary>The scenario was deleted (SC16): nothing of it is sent any more.</summary>
    public void Forget(Guid key)
    {
        _forgotten.Add(key);
        _waiting.Remove(key);
        _unsent.Remove(key);
        _failing.Remove(key);
        _refused.Remove(key);
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Forgets the scenario and completes once no request of it is on its way any more, so a save that was being sent
    /// cannot write the file again after it was deleted (SC16).
    /// </summary>
    public async Task ForgetAsync(Guid key)
    {
        Forget(key);
        if (_running.Contains(key))
        {
            if (!_stopped.TryGetValue(key, out var stopped))
            {
                stopped = new TaskCompletionSource();
                _stopped[key] = stopped;
            }

            await stopped.Task;
        }
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

    private async Task RunAsync(Guid key)
    {
        _running.Add(key);
        var attempt = 0;
        try
        {
            while (_waiting.Remove(key, out var scenario))
            {
                var outcome = await SendAsync(WithId(key, scenario));
                if (_forgotten.Contains(key))
                {
                    break;
                }

                if (outcome.Result == SaveResult.Retry)
                {
                    _failing[key] = outcome.Message ?? "The server is not reachable.";
                    StateChanged?.Invoke();

                    // Tried again, unless a newer change came meanwhile: that one is sent instead.
                    _waiting.TryAdd(key, scenario);
                    await _delay(Backoff[Math.Min(attempt++, Backoff.Length - 1)]);
                    continue;
                }

                attempt = 0;
                _failing.Remove(key);
                if (outcome.Result == SaveResult.Refused)
                {
                    _refused[key] = outcome.Message ?? "The server refused the scenario.";
                }
                else if (outcome.Saved != null)
                {
                    if (outcome.Saved.Id != null)
                    {
                        _ids[key] = outcome.Saved.Id;
                    }

                    // Confirmed only when nothing newer is waiting.
                    if (_unsent.TryGetValue(key, out var newest) && ReferenceEquals(newest, scenario))
                    {
                        _unsent.Remove(key);
                    }

                    Saved?.Invoke(key, outcome.Saved);
                }

                StateChanged?.Invoke();
            }
        }
        finally
        {
            _running.Remove(key);
            if (_stopped.Remove(key, out var stopped))
            {
                stopped.SetResult();
            }

            if (_running.Count == 0 && _idle != null)
            {
                var idle = _idle;
                _idle = null;
                idle.SetResult();
            }

            StateChanged?.Invoke();
        }
    }

    private ExpertScenarioRecord WithId(Guid key, ExpertScenarioRecord scenario)
    {
        return _ids.TryGetValue(key, out var id) ? scenario with { Id = id } : scenario;
    }

    private async Task<ScenarioSaveOutcome> SendAsync(ExpertScenarioRecord scenario)
    {
        try
        {
            return await _remote.SaveAsync(scenario);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new ScenarioSaveOutcome(SaveResult.Retry, Message: "The server is not reachable.");
        }
    }
}
