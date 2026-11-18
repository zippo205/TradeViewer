using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.UnitTests.Application.Assets;

/// <summary>In-memory ports for the handler tests.</summary>
internal sealed class FakeAssetRepository : IAssetRepository
{
    public List<Asset> Assets { get; } = new List<Asset>();

    public List<(string Source, string Symbol)> Pending { get; } = new List<(string, string)>();

    public Task<IReadOnlyList<Asset>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Asset>>(Assets.ToList());
    }

    public Task<Asset?> GetAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        return Task.FromResult(Assets.FirstOrDefault(asset => asset.Source == source && asset.Symbol == symbol));
    }

    public Task<Asset?> AddAsync(SymbolInfo info, AssetHistory history, CancellationToken cancellationToken)
    {
        if (Assets.Any(asset => asset.Source == info.Source && asset.Symbol == info.Symbol))
        {
            return Task.FromResult<Asset?>(null);
        }

        var added = new Asset(info, history, Assets.Count + 1, true);
        Assets.Add(added);
        return Task.FromResult<Asset?>(added);
    }

    public Task SetHistoryAsync(string source, string symbol, AssetHistory history, bool subscriptionSent, CancellationToken cancellationToken)
    {
        var index = Assets.FindIndex(asset => asset.Source == source && asset.Symbol == symbol);
        Assets[index] = Assets[index] with { History = history, SubscriptionSent = subscriptionSent };
        return Task.CompletedTask;
    }

    public Task SetSubscriptionSentAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        var index = Assets.FindIndex(asset => asset.Source == source && asset.Symbol == symbol);
        Assets[index] = Assets[index] with { SubscriptionSent = true };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Asset>> GetUnsentSubscriptionsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Asset>>(Assets.Where(asset => !asset.SubscriptionSent).ToList());
    }

    public Task<bool> RemoveAsync(string source, string symbol, bool unsubscribePending, CancellationToken cancellationToken)
    {
        var removed = Assets.RemoveAll(asset => asset.Source == source && asset.Symbol == symbol) > 0;
        if (removed && unsubscribePending)
        {
            Pending.Add((source, symbol));
        }

        return Task.FromResult(removed);
    }

    public Task<IReadOnlyList<(string Source, string Symbol)>> GetPendingUnsubscriptionsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<(string, string)>>(Pending.ToList());
    }

    public Task<bool> SetOrderAsync(AssetClass assetClass, IReadOnlyList<(string Source, string Symbol)> order, CancellationToken cancellationToken)
    {
        var current = Assets.Where(asset => asset.Info.AssetClass == assetClass).Select(asset => (asset.Source, asset.Symbol)).ToList();
        if (!WatchlistOrder.IsValid(current, order))
        {
            return Task.FromResult(false);
        }

        for (var index = 0; index < order.Count; index++)
        {
            var at = Assets.FindIndex(asset => asset.Source == order[index].Source && asset.Symbol == order[index].Symbol);
            Assets[at] = Assets[at] with { Position = index + 1 };
        }

        Assets.Sort((a, b) => a.Info.AssetClass != b.Info.AssetClass ? a.Info.AssetClass.CompareTo(b.Info.AssetClass) : a.Position.CompareTo(b.Position));
        return Task.FromResult(true);
    }

    public Task ClearPendingUnsubscriptionAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        Pending.Remove((source, symbol));
        return Task.CompletedTask;
    }
}

internal sealed class FakeDownloader : IDownloaderClient
{
    public bool Enabled { get; set; } = true;

    public Error? LookupError { get; set; }

    public Error? SubscribeError { get; set; }

    public Error? UnsubscribeError { get; set; }

    public Error? StatusError { get; set; }

    public SymbolInfo? LookupInfo { get; set; }

    /// <summary>The answer of the catalog; an empty page when not set.</summary>
    public CatalogPage? Catalog { get; set; }

    public Error? SearchError { get; set; }

    /// <summary>The ticker of the last subscription.</summary>
    public string? SubscribedTicker { get; private set; }

    public SymbolStatus Status { get; set; } = new SymbolStatus(0, true, Array.Empty<TimeframeStatus>());

    public SymbolChanges Changes { get; set; } = new SymbolChanges(0, true, Array.Empty<string>());

    /// <summary>The calls in their order, e.g. "subscribe binance/BTCUSDT".</summary>
    public List<string> Calls { get; } = new List<string>();

    public AssetHistory? SubscribedHistory { get; private set; }

    public long? ChangesAfter { get; private set; }

    public Task<Result<CatalogPage>> SearchAsync(string source, string text, int limit, CancellationToken cancellationToken)
    {
        Calls.Add("search " + source + " " + text + " " + limit);
        return Task.FromResult(SearchError != null
            ? Result.Failure<CatalogPage>(SearchError)
            : Result.Success(Catalog ?? new CatalogPage(DateTimeOffset.UnixEpoch, 0, false, Array.Empty<CatalogEntry>())));
    }

    public Task<Result<SymbolLookup>> LookupAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        Calls.Add("lookup " + source + "/" + symbol);
        if (LookupError != null)
        {
            return Task.FromResult(Result.Failure<SymbolLookup>(LookupError));
        }

        var info = LookupInfo ?? new SymbolInfo(source, symbol, symbol + " (Downloader)", AssetClass.Crypto, 0.5, Session.ContinuousUtc, Timeframes.All);
        return Task.FromResult(Result.Success(new SymbolLookup(info, new Dictionary<Timeframe, DateOnly>())));
    }

    public Task<Result<SubscriptionResult>> SubscribeAsync(string source, string symbol, string ticker, AssetHistory history, CancellationToken cancellationToken)
    {
        Calls.Add("subscribe " + source + "/" + symbol);
        SubscribedTicker = ticker;
        if (SubscribeError != null)
        {
            return Task.FromResult(Result.Failure<SubscriptionResult>(SubscribeError));
        }

        SubscribedHistory = history;
        if (!OwnSubscriptions.Contains((source, symbol)))
        {
            OwnSubscriptions.Add((source, symbol));
        }

        return Task.FromResult(Result.Success(new SubscriptionResult(Array.Empty<TimeframeSubscription>())));
    }

    public Task<Result<Unit>> UnsubscribeAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        Calls.Add("unsubscribe " + source + "/" + symbol);
        if (UnsubscribeError == null)
        {
            OwnSubscriptions.Remove((source, symbol));
        }

        return Task.FromResult(UnsubscribeError != null ? Result.Failure<Unit>(UnsubscribeError) : Result.Success());
    }

    /// <summary>What the Downloader holds as subscriptions of TradeViewer; subscribe and unsubscribe change it.</summary>
    public List<(string Source, string Symbol)> OwnSubscriptions { get; } = new List<(string, string)>();

    public Error? SubscriptionsError { get; set; }

    public Task<Result<IReadOnlyList<(string Source, string Symbol)>>> GetOwnSubscriptionsAsync(CancellationToken cancellationToken)
    {
        Calls.Add("subscriptions");
        return Task.FromResult(SubscriptionsError != null
            ? Result.Failure<IReadOnlyList<(string, string)>>(SubscriptionsError)
            : Result.Success<IReadOnlyList<(string, string)>>(OwnSubscriptions.ToList()));
    }

    public Task<Result<SymbolStatus>> GetSymbolStatusAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        Calls.Add("status " + source + "/" + symbol);
        return Task.FromResult(StatusError != null ? Result.Failure<SymbolStatus>(StatusError) : Result.Success(Status));
    }

    public Task<Result<SymbolChanges>> GetChangesAsync(string source, string symbol, long after, CancellationToken cancellationToken)
    {
        Calls.Add("changes " + source + "/" + symbol);
        ChangesAfter = after;
        return Task.FromResult(Result.Success(Changes));
    }
}

internal sealed class FakeImportQueue : IImportQueue
{
    /// <summary>What was asked for, e.g. "rescan binance/BTCUSDT".</summary>
    public List<string> Work { get; } = new List<string>();

    public List<string> Files { get; } = new List<string>();

    public ImportProgress Progress { get; set; } = ImportProgress.None;

    public void Rescan(string source, string symbol)
    {
        Work.Add("rescan " + source + "/" + symbol);
    }

    public void Rebuild(string source, string symbol)
    {
        Work.Add("rebuild " + source + "/" + symbol);
    }

    public void ImportFiles(string source, string symbol, IReadOnlyList<string> files)
    {
        Work.Add("files " + source + "/" + symbol);
        Files.AddRange(files);
    }

    public void Delete(string source, string symbol)
    {
        Work.Add("delete " + source + "/" + symbol);
    }

    public ImportProgress GetProgress(string source, string symbol)
    {
        return Progress;
    }
}

internal sealed class FakeCandleStore : ICandleStore
{
    public Dictionary<SeriesKey, List<Candle>> Candles { get; } = new Dictionary<SeriesKey, List<Candle>>();

    public Dictionary<(string, string), SyncState> SyncStates { get; } = new Dictionary<(string, string), SyncState>();

    public List<ImportLogEntry> Errors { get; } = new List<ImportLogEntry>();

    public void Import(SeriesKey series, IReadOnlyList<FileImport> files)
    {
        throw new NotSupportedException();
    }

    public IReadOnlyDictionary<string, ImportLogEntry> GetImportLog(SeriesKey series)
    {
        return new Dictionary<string, ImportLogEntry>();
    }

    public void RemoveFiles(SeriesKey series, IReadOnlyList<CandleFile> files)
    {
        throw new NotSupportedException();
    }

    public IReadOnlyList<Candle> GetCandles(SeriesKey series, long? before, int limit)
    {
        var all = Candles.TryGetValue(series, out var candles) ? candles : new List<Candle>();
        var older = all.Where(candle => before == null || candle.Time < before.Value).ToList();
        return older.Skip(Math.Max(0, older.Count - limit)).ToList();
    }

    public IReadOnlyDictionary<SeriesKey, IReadOnlyList<Candle>> GetNewestCandles(IReadOnlyList<SeriesKey> series, int count)
    {
        return series.Distinct().ToDictionary(key => key, key => GetCandles(key, null, count));
    }

    public IReadOnlyList<Candle> GetCandlesFrom(SeriesKey series, long from)
    {
        return Candles.TryGetValue(series, out var candles) ? candles.Where(candle => candle.Time >= from).ToList() : new List<Candle>();
    }

    public IReadOnlyList<SeriesRange> GetRanges(string source, string symbol)
    {
        return Candles
            .Where(pair => pair.Key.Source == source && pair.Key.Symbol == symbol && pair.Value.Count > 0)
            .Select(pair => new SeriesRange(pair.Key.Timeframe, pair.Value[0].Time, pair.Value[pair.Value.Count - 1].Time))
            .OrderBy(range => Timeframes.Seconds(range.Timeframe))
            .ToList();
    }

    public IReadOnlyList<ImportLogEntry> GetImportErrors(string source, string symbol)
    {
        return Errors;
    }

    public IReadOnlyList<(string Source, string Symbol)> GetAssets()
    {
        return Candles.Keys.Select(key => (key.Source, key.Symbol)).Distinct().ToList();
    }

    public void DeleteAsset(string source, string symbol)
    {
        throw new NotSupportedException();
    }

    public SyncState? GetSyncState(string source, string symbol)
    {
        return SyncStates.TryGetValue((source, symbol), out var state) ? state : null;
    }

    public void SetSyncState(string source, string symbol, SyncState state)
    {
        SyncStates[(source, symbol)] = state;
    }
}

internal sealed class FakeFolders : IAssetFolders
{
    public HashSet<string> Symbols { get; } = new HashSet<string>();

    public bool HasFolder(string source, string symbol)
    {
        return Symbols.Contains(source + "/" + symbol);
    }
}

internal sealed class FakeHistorySettings : IHistorySettingsRepository
{
    public HistorySettings Settings { get; set; } = HistorySettings.Initial;

    public Task<HistorySettings> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Settings);
    }

    public Task SetAsync(HistorySettings settings, CancellationToken cancellationToken)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}

internal sealed class FixedClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}

internal sealed class FakeNotifier : TradeViewer.Application.Abstractions.Realtime.IWorkspaceNotifier
{
    /// <summary>The events in their order, e.g. "AssetStatusChanged binance/BTCUSDT".</summary>
    public List<string> Events { get; } = new List<string>();

    public Task CandlesUpdatedAsync(string source, string symbol, IReadOnlyList<string> timeframes)
    {
        Events.Add("CandlesUpdated " + source + "/" + symbol + " " + string.Join(",", timeframes));
        return Task.CompletedTask;
    }

    public Task AssetStatusChangedAsync(string source, string symbol)
    {
        Events.Add("AssetStatusChanged " + source + "/" + symbol);
        return Task.CompletedTask;
    }

    public Task DownloaderStatusChangedAsync(bool reachable)
    {
        Events.Add("DownloaderStatusChanged " + reachable);
        return Task.CompletedTask;
    }

    public Task DrawingsChangedAsync(string source, string symbol, string window)
    {
        Events.Add("DrawingsChanged " + source + "/" + symbol + " " + window);
        return Task.CompletedTask;
    }

    public Task ChartSettingsChangedAsync(string source, string symbol, string window)
    {
        Events.Add("ChartSettingsChanged " + source + "/" + symbol + " " + window);
        return Task.CompletedTask;
    }
}
