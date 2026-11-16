using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Watchlists.Commands;
using TradeViewer.Application.Features.Watchlists.Handlers;
using TradeViewer.Application.Features.Watchlists.Queries;
using TradeViewer.Contracts.Watchlists;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.UnitTests.Application.Assets;

namespace TradeViewer.UnitTests.Application.Watchlists;

/// <summary>AS1 and AS6 at the level of the handlers, with in-memory ports.</summary>
public class WatchlistHandlersTests
{
    private const long Nov14 = 1794614400; // 2026-11-14 00:00 UTC
    private const long Nov15 = Nov14 + 86400;

    private readonly FakeAssetRepository _assets = new FakeAssetRepository();
    private readonly FakeCandleStore _store = new FakeCandleStore();
    private readonly FakeImportQueue _imports = new FakeImportQueue();

    [Fact]
    public async Task There_are_always_five_lists_and_an_asset_is_in_the_list_of_its_class()
    {
        Add("binance", "BTCUSDT", AssetClass.Crypto);
        Add("yahoo", "TSLA", AssetClass.Stock);
        Add("binance", "ETHUSDT", AssetClass.Crypto);

        var lists = (await GetAsync()).Value;

        Assert.Equal(new[] { "crypto", "stock", "etf", "forex", "commodity" }, lists.Select(list => list.AssetClass));
        Assert.Equal(new[] { "Crypto", "Stocks", "ETFs", "Forex", "Commodities" }, lists.Select(list => list.Title));
        Assert.Equal(new[] { "BTCUSDT", "ETHUSDT" }, lists[0].Rows.Select(row => row.Symbol));
        Assert.Equal(new[] { ("yahoo", "TSLA") }, lists[1].Rows.Select(row => (row.Source, row.Symbol)));
        Assert.All(lists.Skip(2), list => Assert.Empty(list.Rows));
    }

    [Fact]
    public async Task A_row_shows_the_last_close_its_time_and_the_change_against_the_previous_daily_close()
    {
        Add("binance", "BTCUSDT", AssetClass.Crypto);
        Candles("BTCUSDT", Timeframe.M15, (Nov15 + 68400, 65000), (Nov15 + 69300, 66000)); // 19:00 and 19:15
        Candles("BTCUSDT", Timeframe.D1, (Nov14 - 86400, 70000), (Nov14, 64000));

        var row = (await GetAsync()).Value[0].Rows[0];

        Assert.Equal(66000, row.LastClose);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(Nov15 + 70200), row.LastCloseTime); // 19:30, when the candle closed
        Assert.Equal(3.125, row.ChangePercent!.Value, 10);
        Assert.Equal(new[] { "15m", "1d" }, row.Timeframes);
        Assert.Equal(0.01, row.TickSize);
        Assert.False(row.Loading);
        Assert.Null(row.Warning);
        Assert.Empty(row.AnalysedBy);
    }

    [Fact]
    public async Task A_row_without_candles_has_no_figures_and_nothing_to_chart()
    {
        Add("binance", "BTCUSDT", AssetClass.Crypto);

        var row = (await GetAsync()).Value[0].Rows[0];

        Assert.Null(row.LastClose);
        Assert.Null(row.LastCloseTime);
        Assert.Null(row.ChangePercent);
        Assert.Empty(row.Timeframes);
    }

    [Fact]
    public async Task A_row_is_loading_while_it_is_imported_or_the_Downloader_downloads_it()
    {
        Add("binance", "BTCUSDT", AssetClass.Crypto);
        Add("binance", "ETHUSDT", AssetClass.Crypto);
        _store.SyncStates[("binance", "ETHUSDT")] = new SyncState(1, Status(pendingJobs: 3, gapReason: null), 1);

        var quiet = (await GetAsync()).Value[0].Rows;
        _imports.Progress = new ImportProgress(true, Array.Empty<TimeframeProgress>());
        var importing = (await GetAsync()).Value[0].Rows;

        Assert.Equal(new[] { false, true }, quiet.Select(row => row.Loading));
        Assert.Equal(new[] { true, true }, importing.Select(row => row.Loading));
    }

    [Fact]
    public async Task The_warning_names_files_that_were_not_imported_and_never_a_gap_of_the_source()
    {
        Add("binance", "BTCUSDT", AssetClass.Crypto);

        // An old outage of the source never goes away: a mark that is always on would mean nothing. It is shown in the chart.
        _store.SyncStates[("binance", "BTCUSDT")] = new SyncState(1, Status(0, "missingUpstream"), 1);
        var gap = (await GetAsync()).Value[0].Rows[0];

        _store.Errors.Add(new ImportLogEntry("BTCUSDT-15m-2026-11-04.csv", 1, 1, 0, "line 7: high is below open or close"));
        var file = (await GetAsync()).Value[0].Rows[0];

        Assert.Null(gap.Warning);
        Assert.Equal("1 file was not imported", file.Warning);
        Assert.Equal("3 files were not imported", GetWatchlistsQueryHandler.Warning(3));
    }

    [Fact]
    public async Task A_new_order_of_one_list_is_saved_and_leaves_the_other_lists()
    {
        Add("binance", "BTCUSDT", AssetClass.Crypto);
        Add("binance", "ETHUSDT", AssetClass.Crypto);
        Add("binance", "SOLUSDT", AssetClass.Crypto);
        Add("yahoo", "TSLA", AssetClass.Stock);

        var result = await ReorderAsync("crypto", ("binance", "SOLUSDT"), ("binance", "BTCUSDT"), ("binance", "ETHUSDT"));
        var lists = (await GetAsync()).Value;

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "SOLUSDT", "BTCUSDT", "ETHUSDT" }, lists[0].Rows.Select(row => row.Symbol));
        Assert.Equal(new[] { "TSLA" }, lists[1].Rows.Select(row => row.Symbol));
    }

    [Fact]
    public async Task An_order_that_does_not_name_exactly_the_assets_of_the_list_is_refused_and_changes_nothing()
    {
        Add("binance", "BTCUSDT", AssetClass.Crypto);
        Add("binance", "ETHUSDT", AssetClass.Crypto);
        Add("yahoo", "TSLA", AssetClass.Stock);

        var missing = await ReorderAsync("crypto", ("binance", "ETHUSDT"));
        var foreign = await ReorderAsync("crypto", ("binance", "ETHUSDT"), ("binance", "BTCUSDT"), ("yahoo", "TSLA"));
        var wrongList = await ReorderAsync("stock", ("binance", "ETHUSDT"), ("binance", "BTCUSDT"));

        Assert.All(new[] { missing, foreign, wrongList }, result => Assert.Equal((ErrorType.Conflict, "watchlist.changed"), (result.Error.Type, result.Error.Code)));
        Assert.Equal(new[] { "BTCUSDT", "ETHUSDT" }, (await GetAsync()).Value[0].Rows.Select(row => row.Symbol));
    }

    [Fact]
    public async Task An_unknown_list_and_a_request_without_assets_are_errors()
    {
        var unknown = await ReorderAsync("bonds");
        var noAssets = await new ReorderWatchlistCommandHandler(_assets).HandleAsync(new ReorderWatchlistCommand("crypto", null), CancellationToken.None);
        var noSymbol = await ReorderAsync("crypto", ("binance", ""));
        var emptyList = await ReorderAsync("forex"); // an empty list keeps its (empty) order

        Assert.Equal(ErrorType.NotFound, unknown.Error.Type);
        Assert.Equal(ErrorType.Validation, noAssets.Error.Type);
        Assert.Equal(ErrorType.Validation, noSymbol.Error.Type);
        Assert.True(emptyList.IsSuccess);
    }

    private void Add(string source, string symbol, AssetClass assetClass)
    {
        var info = new SymbolInfo(source, symbol, symbol, assetClass, 0.01, Session.ContinuousUtc, new[] { Timeframe.M15, Timeframe.D1 });
        var history = new AssetHistory(new Dictionary<Timeframe, DateOnly?> { [Timeframe.M15] = null, [Timeframe.D1] = null });
        var position = _assets.Assets.Count(asset => asset.Info.AssetClass == assetClass) + 1;
        _assets.Assets.Add(new Asset(info, history, position, true));
    }

    private void Candles(string symbol, Timeframe timeframe, params (long Time, double Close)[] candles)
    {
        _store.Candles[new SeriesKey("binance", symbol, timeframe)] =
            candles.Select(candle => new Candle(candle.Time, candle.Close, candle.Close + 1, candle.Close - 1, candle.Close, 1)).ToList();
    }

    private static SymbolStatus Status(int pendingJobs, string? gapReason)
    {
        var gaps = gapReason == null
            ? Array.Empty<CoverageGap>()
            : new[] { new CoverageGap(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(4), gapReason) };

        return new SymbolStatus(1, true, new[] { new TimeframeStatus(Timeframe.M15, null, null, null, pendingJobs, gaps, null) });
    }

    private Task<Result<IReadOnlyList<WatchlistResponse>>> GetAsync()
    {
        return new GetWatchlistsQueryHandler(_assets, _store, _imports).HandleAsync(new GetWatchlistsQuery(), CancellationToken.None);
    }

    private Task<Result<Unit>> ReorderAsync(string assetClass, params (string Source, string Symbol)[] order)
    {
        var entries = order.Select(item => new WatchlistEntry(item.Source, item.Symbol)).ToList();
        return new ReorderWatchlistCommandHandler(_assets).HandleAsync(new ReorderWatchlistCommand(assetClass, entries), CancellationToken.None);
    }
}
