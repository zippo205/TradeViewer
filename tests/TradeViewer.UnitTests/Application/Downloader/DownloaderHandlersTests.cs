using Microsoft.Extensions.Logging.Abstractions;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Downloader.Handlers;
using TradeViewer.Application.Features.Downloader.Queries;
using TradeViewer.Contracts.Downloader;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.MarketData.Downloader;
using TradeViewer.UnitTests.Application.Assets;

namespace TradeViewer.UnitTests.Application.Downloader;

/// <summary>DL1-DL4 at the level of the handlers: the state, the dot, the subscriptions, the coverage of an asset.</summary>
public class DownloaderHandlersTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 18, 9, 31, 0, TimeSpan.Zero);

    private readonly FakeDownloader _downloader = new FakeDownloader();
    private readonly FakeAssetRepository _assets = new FakeAssetRepository();
    private readonly FakeCandleStore _store = new FakeCandleStore();
    private readonly FakeNotifier _notifier = new FakeNotifier();
    private readonly FixedClock _clock = new FixedClock { Now = Now };
    private readonly DownloaderReachability _reachability;

    public DownloaderHandlersTests()
    {
        _reachability = new DownloaderReachability(_notifier, _clock, NullLogger<DownloaderReachability>.Instance);
        var info = new SymbolInfo("binance", "BTCUSDT", "BTC/USDT", AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
        _assets.Assets.Add(new Asset(info, new AssetHistory(new Dictionary<Timeframe, DateOnly?> { [Timeframe.M15] = null }), 1, true));
    }

    [Fact]
    public async Task A_Downloader_that_answers_well_is_green()
    {
        _downloader.ServiceStatus = Service();

        var status = await StatusAsync();

        Assert.True(status.Enabled);
        Assert.True(status.Reachable);
        Assert.Equal("green", status.Light);
        Assert.Null(status.UnreachableSince);
        Assert.Equal(Now, status.CheckedAt);
        Assert.Equal("1.4.0", status.Service!.Version);
        Assert.Equal(new[] { "DownloaderStatusChanged True" }, _notifier.Events); // the dots learn of it
    }

    [Theory]
    [InlineData("rateLimited", "ok", "yellow")]
    [InlineData("unavailable", "ok", "yellow")]
    [InlineData("ok", "partial", "yellow")]
    [InlineData("ok", "failed", "yellow")]
    [InlineData("ok", "interrupted", "yellow")]
    [InlineData("ok", "running", "green")]
    [InlineData("ok", "skipped", "green")]
    public async Task A_source_or_a_cycle_that_is_not_ok_makes_it_yellow(string source, string cycle, string light)
    {
        _downloader.ServiceStatus = Service(source, cycle);

        Assert.Equal(light, (await StatusAsync()).Light);
    }

    [Fact]
    public async Task A_Downloader_that_does_not_answer_is_red_and_says_since_when()
    {
        _downloader.ServiceStatus = Service();
        await StatusAsync();
        _downloader.ServiceStatus = null; // the fake fails as when the Downloader cannot be reached
        _clock.Now = Now.AddMinutes(1);
        await StatusAsync();
        _clock.Now = Now.AddMinutes(2);

        var status = await StatusAsync();

        Assert.False(status.Reachable);
        Assert.Equal("red", status.Light);
        Assert.Equal(Now.AddMinutes(1), status.UnreachableSince);
        Assert.Equal(Now.AddMinutes(2), status.CheckedAt);
        Assert.Equal("The Downloader cannot be reached.", status.Problem);
        Assert.Null(status.Service);
    }

    [Fact]
    public async Task Switched_off_nothing_is_asked_and_the_dot_is_off()
    {
        _downloader.Enabled = false;

        var status = await StatusAsync();

        Assert.False(status.Enabled);
        Assert.Equal("off", status.Light);
        Assert.Empty(_downloader.Calls);
        Assert.Equal(ErrorType.Unavailable, (await SubscriptionsAsync()).Error.Type);
    }

    [Fact]
    public void Not_asked_yet_is_off_and_an_answer_that_cannot_be_read_is_yellow()
    {
        Assert.Equal("off", DownloaderStatusRules.LightOf(true, null, null));
        Assert.Equal("yellow", DownloaderStatusRules.LightOf(true, true, null));
        Assert.Equal("red", DownloaderStatusRules.LightOf(true, false, Service()));
        Assert.Equal("off", DownloaderStatusRules.LightOf(false, true, Service()));
    }

    [Fact]
    public async Task The_subscriptions_mark_TradeViewer_and_the_assets_of_the_workspace()
    {
        _downloader.ServiceStatus = Service();
        _downloader.Subscriptions = new[]
        {
            new MergedSubscription("yahoo", "BRK-B", "BRK.B", new[] { "ewts" }, new[] { new MergedTimeframe(Timeframe.D1, new DateOnly(2019, 1, 1), new[] { "ewts" }) }),
            new MergedSubscription("binance", "BTCUSDT", "BTCUSDT", new[] { "ewts", "tradeviewer-test" }, new[] { new MergedTimeframe(Timeframe.M15, null, new[] { "tradeviewer-test" }) })
        };

        var result = (await SubscriptionsAsync()).Value;

        Assert.Equal("tradeviewer-test", result.OwnClient);
        Assert.Equal(new[] { "binance/BTCUSDT", "yahoo/BRK-B" }, result.Subscriptions.Select(s => s.Source + "/" + s.Symbol));
        Assert.Equal((true, true), (result.Subscriptions[0].Own, result.Subscriptions[0].InWorkspace));
        Assert.Equal((false, false), (result.Subscriptions[1].Own, result.Subscriptions[1].InWorkspace));
        var daily = Assert.Single(result.Subscriptions[1].Timeframes);
        Assert.Equal(("1d", "2019-01-01"), (daily.Timeframe, daily.WantedFrom));
        Assert.Equal(new[] { "ewts" }, daily.SubscribedBy);
        Assert.Null(result.Subscriptions[0].Timeframes[0].WantedFrom);
    }

    [Fact]
    public async Task The_subscriptions_of_an_unreachable_Downloader_are_unavailable_and_it_turns_red()
    {
        var result = await SubscriptionsAsync();

        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
        Assert.False(_reachability.Current.Reachable);
    }

    [Fact]
    public async Task The_coverage_of_an_asset_is_asked_now()
    {
        _downloader.ServiceStatus = Service();
        _downloader.Status = Status(300);
        _store.Errors.Add(new ImportLogEntry("15m/BTCUSDT-15m-2026-11-03.csv", 10, 0, 0, "row 7: high below low"));

        var asset = (await AssetAsync("BTCUSDT")).Value;

        Assert.True(asset.Live);
        Assert.Null(asset.ReadAt);
        Assert.Equal(300, asset.LastChange);
        var timeframe = Assert.Single(asset.Timeframes);
        Assert.Equal(("15m", "2026-08-18", 2), (timeframe.Timeframe, timeframe.WantedFrom, timeframe.PendingJobs));
        Assert.Equal("missingUpstream", Assert.Single(timeframe.Gaps).Reason);
        Assert.Equal(Now.AddMinutes(-1), timeframe.LastChangedAt);
        Assert.Equal(new DownloaderImportErrorResponse("15m/BTCUSDT-15m-2026-11-03.csv", "row 7: high below low"), Assert.Single(asset.ImportErrors));
    }

    [Fact]
    public async Task Without_an_answer_the_last_status_of_the_sync_is_shown_with_its_time()
    {
        _downloader.StatusError = Error.Unavailable("downloader.unreachable", "The Downloader cannot be reached.");
        _store.SyncStates[("binance", "BTCUSDT")] = new SyncState(250, Status(250), Now.AddHours(-3).ToUnixTimeSeconds());

        var asset = (await AssetAsync("BTCUSDT")).Value;

        Assert.False(asset.Live);
        Assert.Equal(Now.AddHours(-3), asset.ReadAt);
        Assert.Equal(250, asset.LastChange);
        Assert.Single(asset.Timeframes);
        Assert.Equal("The Downloader cannot be reached.", asset.Problem);
        Assert.False(_reachability.Current.Reachable);
    }

    [Fact]
    public async Task Nothing_known_without_an_answer_is_an_empty_coverage_not_an_error()
    {
        _downloader.Enabled = false;

        var asset = (await AssetAsync("BTCUSDT")).Value;

        Assert.False(asset.Live);
        Assert.Null(asset.ReadAt);
        Assert.Empty(asset.Timeframes);
        Assert.Contains("switched off", asset.Problem);
    }

    [Fact]
    public async Task A_symbol_the_Downloader_has_no_subscription_for_says_so_and_an_unknown_asset_is_not_found()
    {
        _downloader.StatusError = Error.NotFound("downloader.notFound", "Unknown symbol binance/BTCUSDT.");

        var asset = (await AssetAsync("BTCUSDT")).Value;
        var unknown = await AssetAsync("ETHUSDT");

        Assert.True(asset.Live);
        Assert.Empty(asset.Timeframes);
        Assert.Equal("Unknown symbol binance/BTCUSDT.", asset.Problem);
        Assert.True(_reachability.Current.Reachable);
        Assert.Equal(ErrorType.NotFound, unknown.Error.Type);
    }

    private static DownloaderServiceStatus Service(string sourceState = "ok", string cycle = "ok")
    {
        return new DownloaderServiceStatus(
            "running",
            Now.AddHours(-12),
            "1.4.0",
            new UpdateCycle(Now.AddMinutes(-1), Now.AddSeconds(-50), cycle, 12, null),
            Now.AddMinutes(14),
            new DownloaderQueue(0, 0, 0, 0, 0),
            new[] { new SourceState("binance", sourceState, Now.AddMinutes(-1), null, null, null) },
            Array.Empty<CatalogState>(),
            null);
    }

    private static SymbolStatus Status(long lastChange)
    {
        var gap = new CoverageGap(Now.AddDays(-20), Now.AddDays(-20).AddHours(2), "missingUpstream");
        return new SymbolStatus(lastChange, true, new[]
        {
            new TimeframeStatus(Timeframe.M15, new DateOnly(2026, 8, 18), Now.AddDays(-92), Now, 2, new[] { gap }, null, Now.AddMinutes(-1))
        });
    }

    private async Task<DownloaderStatusResponse> StatusAsync()
    {
        return (await new GetDownloaderStatusQueryHandler(_downloader, _reachability).HandleAsync(new GetDownloaderStatusQuery(), CancellationToken.None)).Value;
    }

    private Task<Result<DownloaderSubscriptionsResponse>> SubscriptionsAsync()
    {
        return new GetDownloaderSubscriptionsQueryHandler(_downloader, _reachability, _assets).HandleAsync(new GetDownloaderSubscriptionsQuery(), CancellationToken.None);
    }

    private Task<Result<DownloaderAssetResponse>> AssetAsync(string symbol)
    {
        return new GetDownloaderAssetQueryHandler(_assets, _downloader, _reachability, _store).HandleAsync(new GetDownloaderAssetQuery("binance", symbol), CancellationToken.None);
    }
}
