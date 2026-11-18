using Microsoft.Extensions.Logging;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Realtime;

namespace TradeViewer.Infrastructure.MarketData.Downloader;

/// <summary>One per application: the sync and the status page report to it from different threads.</summary>
public sealed class DownloaderReachability : IDownloaderReachability
{
    private readonly object _lock = new object();
    private readonly IWorkspaceNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly ILogger<DownloaderReachability> _logger;
    private DownloaderReachabilityState _state = DownloaderReachabilityState.Unknown;

    public DownloaderReachability(IWorkspaceNotifier notifier, TimeProvider time, ILogger<DownloaderReachability> logger)
    {
        _notifier = notifier;
        _time = time;
        _logger = logger;
    }

    public DownloaderReachabilityState Current
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public async Task ReportAsync(bool reachable, string? reason)
    {
        var now = _time.GetUtcNow();
        bool changed;
        lock (_lock)
        {
            changed = _state.Reachable != reachable;
            _state = reachable
                ? new DownloaderReachabilityState(true, null, now, null)
                : new DownloaderReachabilityState(false, changed ? now : _state.UnreachableSince ?? now, now, reason);
        }

        if (!changed)
        {
            return;
        }

        // Logged once per change, not with every poll.
        if (reachable)
        {
            _logger.LogInformation("The Downloader is reachable.");
        }
        else
        {
            _logger.LogWarning("The Downloader is not reachable ({Reason}). Charts keep working with the stored candles.", reason);
        }

        await _notifier.DownloaderStatusChangedAsync(reachable);
    }
}
