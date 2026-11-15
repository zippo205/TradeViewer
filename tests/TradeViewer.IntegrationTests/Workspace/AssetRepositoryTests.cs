using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.Workspace;

namespace TradeViewer.IntegrationTests.Workspace;

/// <summary>The assets in workspace.db (AS2, AS3, AS5) and the default history (AS3).</summary>
public class AssetRepositoryTests : IDisposable
{
    private static readonly SymbolInfo Btc = Symbol("BTCUSDT", "BTC/USDT", AssetClass.Crypto, 0.01);
    private static readonly SymbolInfo Eth = Symbol("ETHUSDT", "ETH/USDT", AssetClass.Crypto, 0.01);
    private static readonly SymbolInfo Tesla = new SymbolInfo(
        "yahoo",
        "TSLA",
        "Tesla",
        AssetClass.Stock,
        0.01,
        new Session(SessionType.Exchange, "America/New_York", "09:30", "16:00"),
        new[] { Timeframe.H1, Timeframe.D1 });

    private static readonly AssetHistory History = HistorySettings.Initial.StartingAt(new DateOnly(2026, 11, 15), Timeframes.All);

    private readonly WorkspaceFixture _workspace = new WorkspaceFixture();

    public AssetRepositoryTests()
    {
        _workspace.Start();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task An_added_asset_is_read_back_with_its_metadata_and_history_also_after_a_restart()
    {
        var added = await _workspace.Assets.AddAsync(Btc, History, CancellationToken.None);
        var stock = await _workspace.Assets.AddAsync(Tesla, new AssetHistory(new Dictionary<Timeframe, DateOnly?> { [Timeframe.D1] = null }), CancellationToken.None);

        _workspace.Start();
        var btc = await _workspace.Assets.GetAsync("binance", "BTCUSDT", CancellationToken.None);
        var tesla = await _workspace.Assets.GetAsync("yahoo", "TSLA", CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(Btc with { SourceTimeframes = btc!.Info.SourceTimeframes }, btc.Info);
        Assert.Equal(Timeframes.All, btc.Info.SourceTimeframes);
        Assert.Equal(Timeframes.All, btc.History.Timeframes);
        foreach (var timeframe in Timeframes.All)
        {
            Assert.Equal(History.StartOf(timeframe), btc.History.StartOf(timeframe));
        }

        Assert.Equal(Tesla.Session, tesla!.Info.Session);
        Assert.Equal(new[] { Timeframe.H1, Timeframe.D1 }, tesla.Info.SourceTimeframes);
        Assert.Equal(new[] { Timeframe.D1 }, tesla.History.Timeframes);
        Assert.Equal(stock!.Position, tesla.Position);
        Assert.Null(await _workspace.Assets.GetAsync("binance", "TSLA", CancellationToken.None));
    }

    [Fact]
    public async Task An_asset_cannot_be_added_twice()
    {
        await _workspace.Assets.AddAsync(Btc, History, CancellationToken.None);

        var second = await _workspace.Assets.AddAsync(Btc with { DisplayName = "other" }, History, CancellationToken.None);

        Assert.Null(second);
        var only = Assert.Single(await _workspace.Assets.GetAllAsync(CancellationToken.None));
        Assert.Equal("BTC/USDT", only.Info.DisplayName);
    }

    [Fact]
    public async Task A_new_asset_goes_to_the_end_of_its_watchlist()
    {
        var btc = await _workspace.Assets.AddAsync(Btc, History, CancellationToken.None);
        var tesla = await _workspace.Assets.AddAsync(Tesla, History, CancellationToken.None);
        var eth = await _workspace.Assets.AddAsync(Eth, History, CancellationToken.None);

        var all = await _workspace.Assets.GetAllAsync(CancellationToken.None);

        Assert.Equal(new[] { 1, 2, 1 }, new[] { btc!.Position, eth!.Position, tesla!.Position });
        Assert.Equal(new[] { "BTCUSDT", "ETHUSDT", "TSLA" }, all.Select(asset => asset.Symbol)); // crypto before stock, then by position
    }

    [Fact]
    public async Task A_changed_history_replaces_the_old_one_and_can_wait_for_the_Downloader()
    {
        await _workspace.Assets.AddAsync(Btc, History, CancellationToken.None);
        await _workspace.Assets.AddAsync(Eth, History, CancellationToken.None);
        Assert.Empty(await _workspace.Assets.GetUnsentSubscriptionsAsync(CancellationToken.None));

        var longer = History.With(Timeframe.M15, new DateOnly(2023, 1, 1));
        await _workspace.Assets.SetHistoryAsync("binance", "BTCUSDT", longer, false, CancellationToken.None);

        var btc = await _workspace.Assets.GetAsync("binance", "BTCUSDT", CancellationToken.None);
        var eth = await _workspace.Assets.GetAsync("binance", "ETHUSDT", CancellationToken.None);
        Assert.Equal(new DateOnly(2023, 1, 1), btc!.History.StartOf(Timeframe.M15));
        Assert.Equal(History.StartOf(Timeframe.H1), btc.History.StartOf(Timeframe.H1));
        Assert.Equal(History.StartOf(Timeframe.M15), eth!.History.StartOf(Timeframe.M15));
        Assert.Equal(new[] { "BTCUSDT" }, (await _workspace.Assets.GetUnsentSubscriptionsAsync(CancellationToken.None)).Select(asset => asset.Symbol));

        await _workspace.Assets.SetSubscriptionSentAsync("binance", "BTCUSDT", CancellationToken.None);
        Assert.Empty(await _workspace.Assets.GetUnsentSubscriptionsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_removed_asset_is_gone_with_its_history()
    {
        await _workspace.Assets.AddAsync(Btc, History, CancellationToken.None);
        await _workspace.Assets.AddAsync(Eth, History, CancellationToken.None);

        var removed = await _workspace.Assets.RemoveAsync("binance", "BTCUSDT", false, CancellationToken.None);
        var again = await _workspace.Assets.RemoveAsync("binance", "BTCUSDT", true, CancellationToken.None);

        Assert.True(removed);
        Assert.False(again);
        Assert.Equal(new[] { "ETHUSDT" }, (await _workspace.Assets.GetAllAsync(CancellationToken.None)).Select(asset => asset.Symbol));
        Assert.Empty(await _workspace.Assets.GetPendingUnsubscriptionsAsync(CancellationToken.None));
        Assert.Equal("5", WorkspaceFixture.Scalar(_workspace.Locations.WorkspaceDatabasePath, "SELECT COUNT(*) FROM asset_history;"));

        // It can be added again, with a fresh history.
        var back = await _workspace.Assets.AddAsync(Btc, new AssetHistory(new Dictionary<Timeframe, DateOnly?> { [Timeframe.D1] = null }), CancellationToken.None);
        Assert.Equal(new[] { Timeframe.D1 }, back!.History.Timeframes);
    }

    [Fact]
    public async Task An_unsubscription_that_could_not_be_sent_is_remembered_until_it_is_done()
    {
        await _workspace.Assets.AddAsync(Btc, History, CancellationToken.None);
        await _workspace.Assets.RemoveAsync("binance", "BTCUSDT", true, CancellationToken.None);

        _workspace.Start();
        Assert.Equal(new[] { ("binance", "BTCUSDT") }, await _workspace.Assets.GetPendingUnsubscriptionsAsync(CancellationToken.None));

        await _workspace.Assets.ClearPendingUnsubscriptionAsync("binance", "BTCUSDT", CancellationToken.None);
        Assert.Empty(await _workspace.Assets.GetPendingUnsubscriptionsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_default_history_is_the_initial_one_until_the_user_changes_it()
    {
        var repository = new HistorySettingsRepository(_workspace.Settings);

        var initial = await repository.GetAsync(CancellationToken.None);
        await repository.SetAsync(
            new HistorySettings(new Dictionary<Timeframe, HistoryDepth>
            {
                [Timeframe.M15] = HistoryDepth.OfDays(30),
                [Timeframe.H1] = HistoryDepth.Complete,
                [Timeframe.H4] = HistoryDepth.OfDays(100)
            }),
            CancellationToken.None);
        _workspace.Start();
        var changed = await new HistorySettingsRepository(_workspace.Settings).GetAsync(CancellationToken.None);

        Assert.Equal(90, initial.DepthOf(Timeframe.M15).Days);
        Assert.True(initial.DepthOf(Timeframe.D1).IsComplete);
        Assert.Equal(30, changed.DepthOf(Timeframe.M15).Days);
        Assert.True(changed.DepthOf(Timeframe.H1).IsComplete);
        Assert.Equal(100, changed.DepthOf(Timeframe.H4).Days);
        Assert.True(changed.DepthOf(Timeframe.W1).IsComplete);
        Assert.Equal("{\"15m\":30,\"1h\":null,\"4h\":100,\"1d\":null,\"1w\":null}", await _workspace.GetAsync(HistorySettingsRepository.Key));
    }

    private static SymbolInfo Symbol(string symbol, string name, AssetClass assetClass, double tickSize)
    {
        return new SymbolInfo("binance", symbol, name, assetClass, tickSize, Session.ContinuousUtc, Timeframes.All);
    }
}
