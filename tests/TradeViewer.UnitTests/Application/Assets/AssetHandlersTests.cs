using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Assets.Commands;
using TradeViewer.Application.Features.Assets.Handlers;
using TradeViewer.Application.Features.Assets.Queries;
using TradeViewer.Application.Features.Assets.Services;
using TradeViewer.Contracts.Assets;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.MarketData.Symbols;

namespace TradeViewer.UnitTests.Application.Assets;

/// <summary>AS2 to AS5 at the level of the handlers, with in-memory ports.</summary>
public class AssetHandlersTests
{
    private readonly EmbeddedSymbolCatalog _catalog = new EmbeddedSymbolCatalog();
    private readonly FakeAssetRepository _assets = new FakeAssetRepository();
    private readonly FakeHistorySettings _settings = new FakeHistorySettings();
    private readonly FakeDownloader _downloader = new FakeDownloader();
    private readonly FakeFolders _folders = new FakeFolders();
    private readonly FakeImportQueue _imports = new FakeImportQueue();
    private readonly FakeCandleStore _store = new FakeCandleStore();
    private readonly FixedClock _clock = new FixedClock();

    [Fact]
    public async Task Adding_asks_the_Downloader_subscribes_stores_and_imports_in_that_order()
    {
        var result = await AddAsync("binance", "BTCUSDT");

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "lookup binance/BTCUSDT", "subscribe binance/BTCUSDT" }, _downloader.Calls);
        Assert.Equal(new[] { "rescan binance/BTCUSDT" }, _imports.Work);

        var asset = Assert.Single(_assets.Assets);
        Assert.Equal("BTCUSDT (Downloader)", asset.Info.DisplayName); // the metadata of the Downloader, not of the list
        Assert.Equal(0.5, asset.Info.TickSize);
        Assert.Equal(new DateOnly(2026, 8, 17), asset.History.StartOf(Timeframe.M15)); // default history from today
        Assert.Null(asset.History.StartOf(Timeframe.D1));
        Assert.Same(asset.History, _downloader.SubscribedHistory);

        Assert.Equal("BTCUSDT", result.Value.Symbol);
        Assert.Equal("crypto", result.Value.AssetClass);
        Assert.Equal(new[] { "15m", "1h", "4h", "1d", "1w" }, result.Value.Timeframes.Select(timeframe => timeframe.Timeframe));
        Assert.Equal("2026-08-17", result.Value.Timeframes[0].HistoryFrom);
        Assert.Null(result.Value.Timeframes[3].HistoryFrom);
    }

    [Fact]
    public async Task The_default_history_of_the_user_is_used()
    {
        _settings.Settings = new HistorySettings(new Dictionary<Timeframe, HistoryDepth> { [Timeframe.M15] = HistoryDepth.OfDays(10) });

        await AddAsync("binance", "BTCUSDT");

        Assert.Equal(new DateOnly(2026, 11, 5), _assets.Assets[0].History.StartOf(Timeframe.M15));
        Assert.Null(_assets.Assets[0].History.StartOf(Timeframe.H1));
    }

    [Fact]
    public async Task An_asset_gets_only_the_timeframes_its_source_provides()
    {
        _downloader.LookupInfo = new SymbolInfo("binance", "BTCUSDT", "BTC", AssetClass.Crypto, 0.01, Session.ContinuousUtc, new[] { Timeframe.H1, Timeframe.D1 });

        await AddAsync("binance", "BTCUSDT");

        Assert.Equal(new[] { Timeframe.H1, Timeframe.D1 }, _assets.Assets[0].History.Timeframes);
    }

    [Fact]
    public async Task A_symbol_the_Downloader_does_not_know_is_rejected_with_its_message()
    {
        _downloader.LookupError = Error.InvalidData("downloader.unknownSymbol", "Symbol 'BTCUSDT' is unknown at source 'binance'.");

        var result = await AddAsync("binance", "BTCUSDT");

        Assert.Equal(ErrorType.InvalidData, result.Error.Type);
        Assert.Equal("Symbol 'BTCUSDT' is unknown at source 'binance'.", result.Error.Message);
        Assert.Empty(_assets.Assets);
        Assert.Empty(_imports.Work);
        Assert.Equal(new[] { "lookup binance/BTCUSDT" }, _downloader.Calls);
    }

    [Fact]
    public async Task Nothing_is_stored_when_the_subscription_fails()
    {
        _downloader.SubscribeError = Error.Unavailable("downloader.unavailable", "Source 'binance' is unreachable.");

        var result = await AddAsync("binance", "BTCUSDT");

        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
        Assert.Empty(_assets.Assets);
        Assert.Empty(_imports.Work);
    }

    [Fact]
    public async Task Only_symbols_of_sources_TradeViewer_can_chart_are_added_and_each_only_once()
    {
        var otherSource = await AddAsync("yahoo", "SPY");
        await AddAsync("binance", "BTCUSDT");
        var twice = await AddAsync("binance", "BTCUSDT");

        Assert.Equal(ErrorType.NotFound, otherSource.Error.Type);
        Assert.Equal((ErrorType.Conflict, "asset.exists"), (twice.Error.Type, twice.Error.Code));
        Assert.Single(_assets.Assets);
        Assert.Equal(new[] { "lookup binance/BTCUSDT", "subscribe binance/BTCUSDT", "lookup binance/BTCUSDT" }, _downloader.Calls); // never subscribed twice
    }

    [Fact]
    public async Task With_the_Downloader_any_symbol_of_its_catalog_is_added_the_lookup_being_the_check()
    {
        // XRPUSDC is not in the fixed list; the Downloader's catalog has it.
        var result = await AddAsync("binance", "XRPUSDC");

        Assert.True(result.IsSuccess);
        Assert.Equal("XRPUSDC", _assets.Assets.Single().Symbol);
    }

    [Fact]
    public async Task Without_the_Downloader_only_symbols_of_the_fixed_list_are_added()
    {
        _downloader.Enabled = false;

        var unknown = await AddAsync("binance", "NOPEUSDT");

        Assert.Equal((ErrorType.NotFound, "symbol.notFound"), (unknown.Error.Type, unknown.Error.Code));
    }

    [Fact]
    public async Task A_symbol_picked_by_its_ticker_is_stored_under_the_folder_name_of_the_lookup_and_subscribed_with_the_ticker()
    {
        _downloader.LookupInfo = new SymbolInfo("binance", "BTCUSDT", "BTC/USDT", AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All, "BTC-TICKER");
        var handler = new AddAssetCommandHandler(_catalog, _assets, _settings, _downloader, _folders, _imports, Status(), _clock);

        var result = await handler.HandleAsync(new AddAssetCommand("binance", "BTCUSDT", "BTC-TICKER"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("lookup binance/BTC-TICKER", _downloader.Calls[0]);
        Assert.Equal("subscribe binance/BTCUSDT", _downloader.Calls[1]);
        Assert.Equal("BTC-TICKER", _downloader.SubscribedTicker);
        Assert.Equal("BTC-TICKER", _assets.Assets.Single().Info.Ticker);
    }

    [Fact]
    public async Task Without_the_Downloader_an_asset_needs_its_folder_and_takes_the_metadata_of_the_list()
    {
        _downloader.Enabled = false;
        _folders.Symbols.Add("binance/BTCUSDT");

        var withFolder = await AddAsync("binance", "BTCUSDT");
        var withoutFolder = await AddAsync("binance", "ETHUSDT");

        Assert.True(withFolder.IsSuccess);
        Assert.Equal("BTC/USDT", _assets.Assets[0].Info.DisplayName);
        Assert.Equal((ErrorType.InvalidData, "asset.noData"), (withoutFolder.Error.Type, withoutFolder.Error.Code));
        Assert.Empty(_downloader.Calls);
        Assert.Equal(new[] { "rescan binance/BTCUSDT" }, _imports.Work);
    }

    [Fact]
    public async Task A_changed_history_is_subscribed_stored_and_imported()
    {
        await AddAsync("binance", "BTCUSDT");
        _downloader.Calls.Clear();
        _imports.Work.Clear();

        var result = await ChangeHistoryAsync(new Dictionary<string, string?> { ["15m"] = "2023-01-01", ["4h"] = null });

        Assert.True(result.IsSuccess);
        var history = _assets.Assets[0].History;
        Assert.Equal(new DateOnly(2023, 1, 1), history.StartOf(Timeframe.M15));
        Assert.Null(history.StartOf(Timeframe.H4));
        Assert.Equal(new DateOnly(2025, 11, 15), history.StartOf(Timeframe.H1)); // not named, unchanged
        Assert.Equal(new[] { "subscribe binance/BTCUSDT" }, _downloader.Calls);
        Assert.Equal(new[] { "rescan binance/BTCUSDT" }, _imports.Work);
        Assert.Equal("2023-01-01", result.Value.Timeframes[0].HistoryFrom);
        Assert.False(result.Value.SubscriptionPending);
    }

    [Fact]
    public async Task A_changed_history_is_kept_when_the_Downloader_is_unreachable_and_sent_again_later()
    {
        await AddAsync("binance", "BTCUSDT");
        _downloader.SubscribeError = Error.Unavailable("downloader.unreachable", "The Downloader cannot be reached.");

        var result = await ChangeHistoryAsync(new Dictionary<string, string?> { ["15m"] = "2023-01-01" });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.SubscriptionPending);
        Assert.Equal(new DateOnly(2023, 1, 1), _assets.Assets[0].History.StartOf(Timeframe.M15));
        Assert.False(_assets.Assets[0].SubscriptionSent);
    }

    [Fact]
    public async Task A_history_the_Downloader_refuses_is_not_stored()
    {
        await AddAsync("binance", "BTCUSDT");
        _imports.Work.Clear();
        _downloader.SubscribeError = Error.Validation("downloader.invalidRequest", "historyFromByTimeframe: must not be in the future.");

        var result = await ChangeHistoryAsync(new Dictionary<string, string?> { ["15m"] = "2023-01-01" });

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(new DateOnly(2026, 8, 17), _assets.Assets[0].History.StartOf(Timeframe.M15));
        Assert.Empty(_imports.Work);
    }

    [Fact]
    public async Task A_history_with_wrong_values_names_the_fields()
    {
        await AddAsync("binance", "BTCUSDT");
        _downloader.Calls.Clear();

        var result = await ChangeHistoryAsync(new Dictionary<string, string?>
        {
            ["15m"] = "01.01.2023",
            ["1h"] = "2026-11-16",
            ["5m"] = "2023-01-01",
            ["1d"] = "2020-01-01"
        });
        var empty = await ChangeHistoryAsync(new Dictionary<string, string?>());

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(new[] { "historyFrom.15m", "historyFrom.1h", "historyFrom.5m" }, result.Error.Fields.Keys.OrderBy(key => key));
        Assert.Equal(ErrorType.Validation, empty.Error.Type);
        Assert.Empty(_downloader.Calls);
        Assert.Null(_assets.Assets[0].History.StartOf(Timeframe.D1));
    }

    [Fact]
    public async Task Removing_ends_the_subscription_and_deletes_asset_and_candles()
    {
        await AddAsync("binance", "BTCUSDT");
        _downloader.Calls.Clear();
        _imports.Work.Clear();

        var result = await new RemoveAssetCommandHandler(_assets, _downloader, _imports)
            .HandleAsync(new RemoveAssetCommand("binance", "BTCUSDT"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_assets.Assets);
        Assert.Empty(_assets.Pending);
        Assert.Equal(new[] { "unsubscribe binance/BTCUSDT" }, _downloader.Calls);
        Assert.Equal(new[] { "delete binance/BTCUSDT" }, _imports.Work);
    }

    [Fact]
    public async Task Removing_works_when_the_Downloader_is_unreachable_and_remembers_the_unsubscription()
    {
        await AddAsync("binance", "BTCUSDT");
        _downloader.UnsubscribeError = Error.Unavailable("downloader.unreachable", "The Downloader cannot be reached.");

        var result = await new RemoveAssetCommandHandler(_assets, _downloader, _imports)
            .HandleAsync(new RemoveAssetCommand("binance", "BTCUSDT"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_assets.Assets);
        Assert.Equal(new[] { ("binance", "BTCUSDT") }, _assets.Pending);
        Assert.Contains("delete binance/BTCUSDT", _imports.Work);
    }

    [Fact]
    public async Task Removing_without_the_Downloader_calls_nothing_and_remembers_nothing()
    {
        _downloader.Enabled = false;
        _folders.Symbols.Add("binance/BTCUSDT");
        await AddAsync("binance", "BTCUSDT");

        await new RemoveAssetCommandHandler(_assets, _downloader, _imports)
            .HandleAsync(new RemoveAssetCommand("binance", "BTCUSDT"), CancellationToken.None);

        Assert.Empty(_downloader.Calls);
        Assert.Empty(_assets.Pending);
        Assert.Empty(_assets.Assets);
    }

    [Fact]
    public async Task Reload_and_rebuild_are_handed_to_the_import_worker()
    {
        await AddAsync("binance", "BTCUSDT");
        _imports.Work.Clear();

        var rescan = await new RescanAssetCommandHandler(_assets, _imports).HandleAsync(new RescanAssetCommand("binance", "BTCUSDT"), CancellationToken.None);
        var rebuild = await new RebuildAssetCommandHandler(_assets, _imports).HandleAsync(new RebuildAssetCommand("binance", "BTCUSDT"), CancellationToken.None);

        Assert.True(rescan.IsSuccess);
        Assert.True(rebuild.IsSuccess);
        Assert.Equal(new[] { "rescan binance/BTCUSDT", "rebuild binance/BTCUSDT" }, _imports.Work);
    }

    [Fact]
    public async Task An_unknown_asset_is_not_found_by_any_handler()
    {
        var results = new[]
        {
            (await new GetAssetQueryHandler(_assets, Status()).HandleAsync(new GetAssetQuery("binance", "BTCUSDT"), CancellationToken.None)).Error,
            (await ChangeHistoryAsync(new Dictionary<string, string?> { ["15m"] = null })).Error,
            (await new RemoveAssetCommandHandler(_assets, _downloader, _imports).HandleAsync(new RemoveAssetCommand("binance", "BTCUSDT"), CancellationToken.None)).Error,
            (await new RescanAssetCommandHandler(_assets, _imports).HandleAsync(new RescanAssetCommand("binance", "BTCUSDT"), CancellationToken.None)).Error,
            (await new RebuildAssetCommandHandler(_assets, _imports).HandleAsync(new RebuildAssetCommand("binance", "BTCUSDT"), CancellationToken.None)).Error
        };

        Assert.All(results, error => Assert.Equal((ErrorType.NotFound, "asset.notFound"), (error.Type, error.Code)));
        Assert.Empty(_imports.Work);
        Assert.Empty(_downloader.Calls);
    }

    [Fact]
    public async Task Without_the_Downloader_the_fixed_list_is_searched_and_marks_what_is_added()
    {
        _downloader.Enabled = false;
        _folders.Symbols.Add("binance/ETHUSDT");
        await AddAsync("binance", "ETHUSDT");
        var handler = new GetSymbolsQueryHandler(_catalog, _assets, _downloader);

        var all = await handler.HandleAsync(new GetSymbolsQuery("binance", null), CancellationToken.None);
        var found = await handler.HandleAsync(new GetSymbolsQuery("binance", "eth"), CancellationToken.None);

        Assert.Equal((13, 13, false, false), (all.Value.Symbols.Count, all.Value.Total, all.Value.Truncated, all.Value.FromCatalog));
        Assert.Equal(new[] { "ETHUSDT" }, all.Value.Symbols.Where(symbol => symbol.Added).Select(symbol => symbol.Symbol));
        Assert.Equal(new SymbolResponse("binance", "ETHUSDT", "ETH/USDT", "crypto", true), Assert.Single(found.Value.Symbols));
    }

    [Fact]
    public async Task With_the_Downloader_its_catalog_is_searched_with_the_ticker_and_the_exchange()
    {
        await AddAsync("binance", "ETHUSDT");
        var refreshed = new DateTimeOffset(2026, 11, 17, 7, 0, 0, TimeSpan.Zero);
        _downloader.Catalog = new CatalogPage(refreshed, 120, true, new[]
        {
            new CatalogEntry("ETHUSDT", "ETHUSDT", "ETH / USDT", "crypto", null),
            new CatalogEntry("NYSE:BRK.B", "BRK-B", "Berkshire Hathaway", "stock", "NYSE")
        });
        var handler = new GetSymbolsQueryHandler(_catalog, _assets, _downloader);

        var result = (await handler.HandleAsync(new GetSymbolsQuery("binance", "  eth "), CancellationToken.None)).Value;

        Assert.Contains("search binance eth 50", _downloader.Calls);
        Assert.Equal((120, true, true, (DateTimeOffset?)refreshed), (result.Total, result.Truncated, result.FromCatalog, result.RefreshedAt));
        Assert.Equal(new SymbolResponse("binance", "ETHUSDT", "ETH / USDT", "crypto", true), result.Symbols[0]);
        Assert.Equal(new SymbolResponse("binance", "BRK-B", "Berkshire Hathaway", "stock", false, "NYSE:BRK.B", "NYSE"), result.Symbols[1]);
    }

    [Fact]
    public async Task Sources_TradeViewer_cannot_chart_and_texts_too_long_for_the_catalog_are_refused()
    {
        var handler = new GetSymbolsQueryHandler(_catalog, _assets, _downloader);

        var yahoo = await handler.HandleAsync(new GetSymbolsQuery("yahoo", "spy"), CancellationToken.None);
        var tooLong = await handler.HandleAsync(new GetSymbolsQuery("binance", new string('x', 41)), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, yahoo.Error.Type);
        Assert.Equal(ErrorType.Validation, tooLong.Error.Type);
        Assert.Empty(_downloader.Calls);
    }

    [Fact]
    public async Task The_catalog_unreachable_is_answered_with_the_message_of_the_Downloader()
    {
        _downloader.SearchError = Error.Unavailable("downloader.unreachable", "The Downloader cannot be reached.");

        var result = await new GetSymbolsQueryHandler(_catalog, _assets, _downloader).HandleAsync(new GetSymbolsQuery("binance", "btc"), CancellationToken.None);

        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
    }

    [Fact]
    public async Task A_symbol_picked_is_looked_up_with_tick_size_session_timeframes_and_earliest_dates()
    {
        var handler = new GetSymbolDetailsQueryHandler(_catalog, _assets, _downloader);

        var details = (await handler.HandleAsync(new GetSymbolDetailsQuery("binance", "SOLUSDT"), CancellationToken.None)).Value;

        Assert.Equal("lookup binance/SOLUSDT", Assert.Single(_downloader.Calls));
        Assert.Equal(("SOLUSDT", "SOLUSDT", "crypto", 0.5, "continuous", "UTC"), (details.Symbol, details.SourceSymbol, details.AssetClass, details.TickSize, details.Session, details.TimeZone));
        Assert.Equal(new[] { "15m", "1h", "4h", "1d", "1w" }, details.Timeframes);
        Assert.False(details.Added);
    }

    [Fact]
    public async Task A_symbol_the_Downloader_does_not_know_is_refused_when_it_is_looked_up()
    {
        _downloader.LookupError = Error.InvalidData("downloader.unknownSymbol", "Symbol 'NOPE' is unknown at source 'binance'.");

        var result = await new GetSymbolDetailsQueryHandler(_catalog, _assets, _downloader).HandleAsync(new GetSymbolDetailsQuery("binance", "NOPE"), CancellationToken.None);

        Assert.Equal("Symbol 'NOPE' is unknown at source 'binance'.", result.Error.Message);
    }

    [Fact]
    public async Task The_list_of_assets_shows_every_asset_with_its_state()
    {
        await AddAsync("binance", "BTCUSDT");
        await AddAsync("binance", "ETHUSDT");
        _store.Candles[new SeriesKey("binance", "BTCUSDT", Timeframe.D1)] = new List<Candle> { new Candle(86400, 1, 2, 1, 2, 1), new Candle(172800, 1, 2, 1, 2, 1) };

        var result = await new GetAssetsQueryHandler(_assets, Status()).HandleAsync(new GetAssetsQuery(), CancellationToken.None);

        Assert.Equal(new[] { "BTCUSDT", "ETHUSDT" }, result.Value.Select(asset => asset.Symbol));
        var daily = result.Value[0].Timeframes.Single(timeframe => timeframe.Timeframe == "1d");
        Assert.True(daily.Chartable);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(86400), daily.FirstCandle);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(172800), daily.LastCandle);
        Assert.All(result.Value[1].Timeframes, timeframe => Assert.False(timeframe.Chartable));
    }

    private AssetStatusReader Status()
    {
        return new AssetStatusReader(_store, _imports);
    }

    private Task<Result<AssetResponse>> AddAsync(string source, string symbol)
    {
        var handler = new AddAssetCommandHandler(_catalog, _assets, _settings, _downloader, _folders, _imports, Status(), _clock);
        return handler.HandleAsync(new AddAssetCommand(source, symbol), CancellationToken.None);
    }

    private Task<Result<AssetResponse>> ChangeHistoryAsync(IReadOnlyDictionary<string, string?> historyFrom)
    {
        var handler = new ChangeAssetHistoryCommandHandler(_assets, _downloader, _imports, Status(), _clock);
        return handler.HandleAsync(new ChangeAssetHistoryCommand("binance", "BTCUSDT", historyFrom), CancellationToken.None);
    }
}
