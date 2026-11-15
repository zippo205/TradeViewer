using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Realtime;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.Assets;
using TradeViewer.Infrastructure.Configuration;

namespace TradeViewer.Infrastructure.MarketData.Sync;

/// <summary>
/// Keeps the candle store up to date with the Downloader (MD2). Per asset it asks for the symbol status when
/// new candles are due, and then in short intervals until the change number has grown and is quiet again, at
/// most for the poll window. A greater change number means: fetch the changed files and import them.
/// It also sends subscriptions and unsubscriptions again that the Downloader could not take earlier.
/// </summary>
public sealed class MarketDataSync
{
    private readonly IAssetRepository _assets;
    private readonly IDownloaderClient _downloader;
    private readonly ICandleStore _store;
    private readonly IImportQueue _imports;
    private readonly IWorkspaceNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly ILogger<MarketDataSync> _logger;
    private readonly TimeSpan _poll;
    private readonly TimeSpan _window;

    private readonly Dictionary<(string Source, string Symbol), Plan> _plans = new Dictionary<(string, string), Plan>();
    private bool? _reachable;

    public MarketDataSync(
        IAssetRepository assets,
        IDownloaderClient downloader,
        ICandleStore store,
        IImportQueue imports,
        IWorkspaceNotifier notifier,
        IOptions<MarketDataOptions> options,
        TimeProvider time,
        ILogger<MarketDataSync> logger)
    {
        _assets = assets;
        _downloader = downloader;
        _store = store;
        _imports = imports;
        _notifier = notifier;
        _time = time;
        _logger = logger;
        _poll = TimeSpan.FromSeconds(options.Value.Downloader.PollSeconds);
        _window = TimeSpan.FromMinutes(options.Value.Downloader.PollWindowMinutes);
    }

    /// <summary>Null before the first answer or failure.</summary>
    public bool? DownloaderReachable => _reachable;

    /// <summary>Does everything that is due now.</summary>
    /// <returns>When to run again.</returns>
    public async Task<DateTimeOffset> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var assets = await _assets.GetAllAsync(cancellationToken);

        // Plans of removed assets go; new assets are checked at once.
        var keys = assets.Select(asset => (asset.Source, asset.Symbol)).ToHashSet();
        foreach (var key in _plans.Keys.Where(key => !keys.Contains(key)).ToList())
        {
            _plans.Remove(key);
        }

        foreach (var asset in assets)
        {
            if (asset.History.Timeframes.Count == 0)
            {
                continue;
            }

            var key = (asset.Source, asset.Symbol);
            if (!_plans.TryGetValue(key, out var plan))
            {
                plan = new Plan { Due = SyncSchedule.LastDue(now, asset.History.Timeframes[0]), NextCheck = now };
                _plans[key] = plan;
            }

            if (now >= plan.NextCheck)
            {
                await CheckAsync(asset, plan, now, cancellationToken);
            }
        }

        if (_reachable != false)
        {
            // Read again: a check above may have found a subscription missing.
            await SendAgainAsync(await _assets.GetAllAsync(cancellationToken), cancellationToken);
        }

        var next = now + _poll;
        foreach (var plan in _plans.Values)
        {
            if (plan.NextCheck < next)
            {
                next = plan.NextCheck;
            }
        }

        // Unsubscriptions of removed assets have no plan; they are tried again with every poll.
        return next > now ? next : now + TimeSpan.FromSeconds(1);
    }

    private async Task CheckAsync(Asset asset, Plan plan, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var smallest = asset.History.Timeframes[0];

        var status = await _downloader.GetSymbolStatusAsync(asset.Source, asset.Symbol, cancellationToken);
        if (status.IsFailure && status.Error.Type == ErrorType.NotFound)
        {
            // The Downloader answers but does not know the symbol: it lost the subscription, or never got one.
            await SetReachableAsync(true, null);
            await SubscribeAgainAsync(asset, cancellationToken);
            plan.NextCheck = now + _poll;
            return;
        }

        if (status.IsFailure)
        {
            // The last known status stays; charts are not affected (NF5). Try again with the next poll.
            await SetReachableAsync(false, status.Error.Message);
            plan.NextCheck = now + _poll;
            return;
        }

        await SetReachableAsync(true, null);

        if (!status.Value.Subscribed)
        {
            // Added while the Downloader was switched off, or the Downloader lost its subscriptions.
            await SubscribeAgainAsync(asset, cancellationToken);
        }

        var stored = _store.GetSyncState(asset.Source, asset.Symbol);
        var lastChange = stored?.LastChange ?? 0;
        var changed = false;

        if (stored == null)
        {
            // First contact (a new asset, or a new candle store): a rescan sees every file written until now.
            _imports.Rescan(asset.Source, asset.Symbol);
            lastChange = status.Value.LastChange;
        }
        else if (status.Value.LastChange > stored.LastChange)
        {
            var changes = await _downloader.GetChangesAsync(asset.Source, asset.Symbol, stored.LastChange, cancellationToken);
            if (changes.IsFailure)
            {
                plan.NextCheck = now + _poll;
                return;
            }

            if (!changes.Value.Complete)
            {
                // The Downloader no longer knows all changes since then: every file counts as changed.
                _imports.Rescan(asset.Source, asset.Symbol);
            }
            else if (changes.Value.Files.Count > 0)
            {
                _imports.ImportFiles(asset.Source, asset.Symbol, changes.Value.Files);
            }

            lastChange = changes.Value.LastChange;
            changed = true;
        }

        _store.SetSyncState(asset.Source, asset.Symbol, new SyncState(lastChange, status.Value, now.ToUnixTimeSeconds()));

        var downloading = IsDownloading(status.Value);
        if (stored == null || changed || downloading != plan.Downloading)
        {
            await _notifier.AssetStatusChangedAsync(asset.Source, asset.Symbol);
        }

        plan.Downloading = downloading;
        plan.SeenChange = plan.SeenChange || changed;

        // Keep polling while history is downloaded, while the change of this due time has not shown yet, and
        // once more after a change (a cycle writes several timeframes one after the other).
        var windowOver = now >= plan.Due + _window;
        var settled = plan.SeenChange && !changed;
        if (!downloading && (settled || windowOver))
        {
            plan.Due = SyncSchedule.NextDue(now, smallest);
            plan.NextCheck = plan.Due;
            plan.SeenChange = false;
        }
        else
        {
            plan.NextCheck = now + _poll;
        }
    }

    /// <summary>Marks the subscription of the asset as not sent; it is sent at the end of this run.</summary>
    private async Task SubscribeAgainAsync(Asset asset, CancellationToken cancellationToken)
    {
        if (asset.SubscriptionSent)
        {
            _logger.LogWarning("The Downloader has no subscription of TradeViewer for {Source}/{Symbol}; it is sent again.", asset.Source, asset.Symbol);
            await _assets.SetHistoryAsync(asset.Source, asset.Symbol, asset.History, false, cancellationToken);
        }
    }

    /// <summary>Subscriptions and unsubscriptions the Downloader could not take when the user asked for them.</summary>
    private async Task SendAgainAsync(IReadOnlyList<Asset> assets, CancellationToken cancellationToken)
    {
        foreach (var asset in assets.Where(asset => !asset.SubscriptionSent))
        {
            var result = await _downloader.SubscribeAsync(asset.Source, asset.Symbol, asset.History, cancellationToken);
            if (result.IsSuccess)
            {
                await _assets.SetSubscriptionSentAsync(asset.Source, asset.Symbol, cancellationToken);
                await _notifier.AssetStatusChangedAsync(asset.Source, asset.Symbol);
                _logger.LogInformation("Subscription of {Source}/{Symbol} sent again and accepted.", asset.Source, asset.Symbol);
            }
        }

        foreach (var (source, symbol) in await _assets.GetPendingUnsubscriptionsAsync(cancellationToken))
        {
            var result = await _downloader.UnsubscribeAsync(source, symbol, cancellationToken);
            if (result.IsSuccess)
            {
                await _assets.ClearPendingUnsubscriptionAsync(source, symbol, cancellationToken);
                _logger.LogInformation("Subscription of the removed asset {Source}/{Symbol} ended at the Downloader.", source, symbol);
            }
        }
    }

    private async Task SetReachableAsync(bool reachable, string? reason)
    {
        if (_reachable == reachable)
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

        _reachable = reachable;
        await _notifier.DownloaderStatusChangedAsync(reachable);
    }

    private static bool IsDownloading(SymbolStatus status)
    {
        return status.Timeframes.Any(timeframe => timeframe.PendingJobs > 0 || timeframe.Gaps.Any(gap => gap.Reason == "pending"));
    }

    private sealed class Plan
    {
        /// <summary>The due time the current polling belongs to.</summary>
        public DateTimeOffset Due { get; set; }

        public DateTimeOffset NextCheck { get; set; }

        public bool SeenChange { get; set; }

        public bool Downloading { get; set; }
    }
}
