namespace TradeViewer.Infrastructure.Workspace;

/// <summary>
/// Lets a restore replace workspace.db while the application runs. Every use of the database holds a shared
/// lease; the restore takes the exclusive one: it waits until all shared leases are returned, and whoever
/// arrives meanwhile waits for the restore instead of failing.
/// A holder of a lease must not ask for a second one: it would wait for itself when a restore is pending.
/// </summary>
public sealed class WorkspaceGate
{
    private readonly SemaphoreSlim _entry = new SemaphoreSlim(1, 1);
    private readonly object _sync = new object();
    private int _users;
    private TaskCompletionSource? _idle;

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _entry.WaitAsync(cancellationToken);
        lock (_sync)
        {
            _users++;
        }

        _entry.Release();
        return new Lease(LeaveShared);
    }

    public async Task<IDisposable> EnterExclusiveAsync(CancellationToken cancellationToken)
    {
        // Holding the entry keeps new users out until the exclusive lease is returned.
        await _entry.WaitAsync(cancellationToken);
        try
        {
            await WaitUntilIdleAsync(cancellationToken);
        }
        catch
        {
            _entry.Release();
            throw;
        }

        return new Lease(() => _entry.Release());
    }

    private Task WaitUntilIdleAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_users == 0)
            {
                return Task.CompletedTask;
            }

            _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _idle.Task.WaitAsync(cancellationToken);
        }
    }

    private void LeaveShared()
    {
        lock (_sync)
        {
            _users--;
            if (_users == 0 && _idle != null)
            {
                _idle.TrySetResult();
                _idle = null;
            }
        }
    }

    private sealed class Lease : IDisposable
    {
        private Action? _release;

        public Lease(Action release)
        {
            _release = release;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _release, null)?.Invoke();
        }
    }
}
