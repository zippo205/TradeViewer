using System.Net;
using System.Text.Json;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.IntegrationTests.MarketData;

/// <summary>The Downloader client against recorded responses: paths, bodies, mapping, and errors as results.</summary>
public class DownloaderClientTests
{
    private const string Subscription = "/api/v1/clients/tradeviewer-test/subscriptions/binance/BTCUSDT";

    private readonly StubDownloader _downloader = new StubDownloader();

    [Fact]
    public async Task A_lookup_returns_the_metadata_and_leaves_out_timeframes_TradeViewer_does_not_know()
    {
        _downloader.Answer("GET /api/v1/sources/binance/symbols/BTCUSDT", HttpStatusCode.OK, "lookup-BTCUSDT.json");

        var result = await _downloader.NewClient().LookupAsync("binance", "BTCUSDT", CancellationToken.None);

        var lookup = result.Value;
        Assert.Equal("binance", lookup.Info.Source);
        Assert.Equal("BTCUSDT", lookup.Info.Symbol);
        Assert.Equal("BTC/USDT", lookup.Info.DisplayName);
        Assert.Equal(AssetClass.Crypto, lookup.Info.AssetClass);
        Assert.Equal(0.01, lookup.Info.TickSize);
        Assert.Equal(Session.ContinuousUtc, lookup.Info.Session);
        Assert.Equal(Timeframes.All, lookup.Info.SourceTimeframes);
        Assert.Equal(new DateOnly(2017, 8, 17), lookup.Earliest[Timeframe.D1]);
        Assert.Equal(2, lookup.Earliest.Count);
    }

    [Fact]
    public async Task A_search_of_the_catalog_returns_its_symbols_and_says_when_more_matched()
    {
        _downloader.Answer("GET /api/v1/sources/binance/symbols?q=btc&limit=50", HttpStatusCode.OK, "catalog-binance-btc.json");

        var result = await _downloader.NewClient().SearchAsync("binance", "btc", 50, CancellationToken.None);

        var page = result.Value;
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 7, 29, 48, TimeSpan.Zero), page.RefreshedAt);
        Assert.Equal(112, page.Total);
        Assert.True(page.Truncated);
        Assert.Equal(new CatalogEntry("BTCUSDT", "BTCUSDT", "BTC / USDT", "crypto", null), page.Symbols[0]);
        Assert.Equal(2, page.Symbols.Count);
    }

    [Fact]
    public async Task A_search_text_is_sent_escaped_and_a_source_without_a_list_yet_says_so()
    {
        _downloader.Answer("GET /api/v1/sources/yahoo/symbols?q=s%26p%20500&limit=10", HttpStatusCode.OK, "catalog-not-loaded.json");

        var result = await _downloader.NewClient().SearchAsync("yahoo", "s&p 500", 10, CancellationToken.None);

        Assert.Null(result.Value.RefreshedAt);
        Assert.Empty(result.Value.Symbols);
    }

    [Fact]
    public async Task A_lookup_by_a_ticker_that_differs_from_the_folder_name_keeps_both()
    {
        _downloader.Answer("GET /api/v1/sources/tradingview/symbols/NYSE%3ABRK.B", HttpStatusCode.OK, "lookup-NYSE-BRK.B.json");

        var result = await _downloader.NewClient().LookupAsync("tradingview", "NYSE:BRK.B", CancellationToken.None);

        var info = result.Value.Info;
        Assert.Equal("BRK-B", info.Symbol);
        Assert.Equal("NYSE:BRK.B", info.SourceSymbol);
        Assert.Equal("NYSE:BRK.B", info.Ticker);
        Assert.Equal(new Session(SessionType.Exchange, "America/New_York", "09:30", "16:00"), info.Session);
        Assert.Equal(AssetClass.Stock, info.AssetClass);
    }

    [Fact]
    public async Task A_lookup_where_ticker_and_folder_name_are_the_same_keeps_no_separate_ticker()
    {
        _downloader.Answer("GET /api/v1/sources/binance/symbols/BTCUSDT", HttpStatusCode.OK, "lookup-BTCUSDT.json");

        var info = (await _downloader.NewClient().LookupAsync("binance", "BTCUSDT", CancellationToken.None)).Value.Info;

        Assert.Null(info.SourceSymbol);
        Assert.Equal("BTCUSDT", info.Ticker);
    }

    [Fact]
    public async Task A_subscription_has_the_folder_name_in_the_path_and_the_ticker_in_the_body()
    {
        _downloader.Answer("PUT /api/v1/clients/tradeviewer-test/subscriptions/tradingview/BRK-B", HttpStatusCode.OK, "subscription-BTCUSDT.json");

        await _downloader.NewClient().SubscribeAsync("tradingview", "BRK-B", "NYSE:BRK.B", MarketDataFixture.Asset("BTCUSDT").History, CancellationToken.None);

        using var body = JsonDocument.Parse(_downloader.LastBody!);
        Assert.Equal("NYSE:BRK.B", body.RootElement.GetProperty("sourceSymbol").GetString());
        Assert.Contains("PUT /api/v1/clients/tradeviewer-test/subscriptions/tradingview/BRK-B", _downloader.Requests);
    }

    [Fact]
    public async Task A_symbol_the_source_does_not_know_is_invalid_data_with_the_message_of_the_Downloader()
    {
        _downloader.Answer("GET /api/v1/sources/binance/symbols/NOPEUSDT", HttpStatusCode.UnprocessableEntity, "problem-unknown-symbol.json");

        var result = await _downloader.NewClient().LookupAsync("binance", "NOPEUSDT", CancellationToken.None);

        Assert.Equal(ErrorType.InvalidData, result.Error.Type);
        Assert.Equal("Symbol 'NOPEUSDT' is unknown at source 'binance'.", result.Error.Message);
    }

    [Fact]
    public async Task A_subscription_sends_every_timeframe_with_its_start_and_an_early_date_for_the_complete_history()
    {
        _downloader.Answer("PUT " + Subscription, HttpStatusCode.OK, "subscription-BTCUSDT.json");
        var history = new AssetHistory(new Dictionary<Timeframe, DateOnly?>
        {
            [Timeframe.M15] = new DateOnly(2023, 1, 1),
            [Timeframe.H1] = new DateOnly(2025, 11, 15),
            [Timeframe.D1] = null
        });

        var result = await _downloader.NewClient().SubscribeAsync("binance", "BTCUSDT", "BTCUSDT", history, CancellationToken.None);

        using var body = JsonDocument.Parse(_downloader.LastBody!);
        Assert.Equal(new[] { "15m", "1h", "1d" }, body.RootElement.GetProperty("timeframes").EnumerateArray().Select(item => item.GetString()));
        var starts = body.RootElement.GetProperty("historyFromByTimeframe");
        Assert.Equal("2023-01-01", starts.GetProperty("15m").GetString());
        Assert.Equal("2025-11-15", starts.GetProperty("1h").GetString());
        Assert.Equal("1970-01-01", starts.GetProperty("1d").GetString());

        Assert.Equal(
            new[]
            {
                new TimeframeSubscription(Timeframe.M15, true, new DateOnly(2023, 1, 1), false),
                new TimeframeSubscription(Timeframe.H1, false, null, false),
                new TimeframeSubscription(Timeframe.D1, true, new DateOnly(2017, 8, 17), true)
            },
            result.Value.Timeframes);
    }

    [Fact]
    public async Task A_subscription_that_fails_because_the_source_is_down_is_unavailable()
    {
        _downloader.Answer("PUT " + Subscription, HttpStatusCode.ServiceUnavailable, "problem-source-unreachable.json");

        var result = await _downloader.NewClient().SubscribeAsync("binance", "BTCUSDT", "BTCUSDT", MarketDataFixture.Asset("BTCUSDT").History, CancellationToken.None);

        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
        Assert.Equal("Source 'binance' is unreachable.", result.Error.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Unsubscribing_is_done_when_the_Downloader_confirms_or_has_no_subscription(HttpStatusCode status)
    {
        _downloader.Answer("DELETE " + Subscription, status, null);

        var result = await _downloader.NewClient().UnsubscribeAsync("binance", "BTCUSDT", CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Unsubscribing_fails_when_the_Downloader_cannot_be_reached()
    {
        _downloader.Fail("DELETE " + Subscription, new HttpRequestException("Connection refused"));

        var result = await _downloader.NewClient().UnsubscribeAsync("binance", "BTCUSDT", CancellationToken.None);

        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
        Assert.Equal("downloader.unreachable", result.Error.Code);
    }

    [Fact]
    public async Task The_symbol_status_carries_the_change_number_coverage_and_gaps()
    {
        _downloader.Answer("GET /api/v1/status/binance/BTCUSDT", HttpStatusCode.OK, "status-BTCUSDT.json");

        var status = (await _downloader.NewClient().GetSymbolStatusAsync("binance", "BTCUSDT", CancellationToken.None)).Value;

        Assert.Equal(48213, status.LastChange);
        Assert.False(status.Subscribed); // subscribedBy names "tradeviewer" and "ewts"; this client is "tradeviewer-test"

        var own = (await _downloader.NewClient(client: "tradeviewer").GetSymbolStatusAsync("binance", "BTCUSDT", CancellationToken.None)).Value;
        Assert.True(own.Subscribed);
        Assert.Equal(new[] { Timeframe.M15, Timeframe.H1 }, status.Timeframes.Select(item => item.Timeframe)); // 1M is unknown here

        var m15 = status.Timeframes[0];
        Assert.Equal(new DateOnly(2019, 1, 1), m15.WantedFrom);
        Assert.Equal(new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero), m15.CoveredFrom);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 15, 15, 0, TimeSpan.Zero), m15.CoveredTo);
        Assert.Equal(0, m15.PendingJobs);
        Assert.Equal(
            new CoverageGap(new DateTimeOffset(2021, 8, 13, 2, 0, 0, TimeSpan.Zero), new DateTimeOffset(2021, 8, 13, 6, 30, 0, TimeSpan.Zero), "missingUpstream"),
            Assert.Single(m15.Gaps));

        var h1 = status.Timeframes[1];
        Assert.Null(h1.CoveredFrom);
        Assert.Equal(12, h1.PendingJobs);
        Assert.Equal("source unreachable", h1.LastError);
    }

    [Fact]
    public async Task The_own_subscriptions_are_those_of_this_client_and_an_unknown_client_has_none()
    {
        _downloader.AnswerText(
            "GET /api/v1/clients/tradeviewer-test/subscriptions",
            HttpStatusCode.OK,
            "[" + StubDownloader.Fixture("subscription-BTCUSDT.json") + "," + StubDownloader.Fixture("subscription-BTCUSDT.json").Replace("BTCUSDT", "ETHUSDT") + "]");
        _downloader.Answer("GET /api/v1/clients/never-seen/subscriptions", HttpStatusCode.NotFound, null);

        var own = await _downloader.NewClient().GetOwnSubscriptionsAsync(CancellationToken.None);
        var unknown = await _downloader.NewClient(client: "never-seen").GetOwnSubscriptionsAsync(CancellationToken.None);

        Assert.Equal(new[] { ("binance", "BTCUSDT"), ("binance", "ETHUSDT") }, own.Value);
        Assert.Empty(unknown.Value);
    }

    [Fact]
    public async Task The_changes_name_every_changed_file_once()
    {
        _downloader.Answer("GET /api/v1/status/binance/BTCUSDT/changes?after=48100", HttpStatusCode.OK, "changes-BTCUSDT.json");

        var changes = (await _downloader.NewClient().GetChangesAsync("binance", "BTCUSDT", 48100, CancellationToken.None)).Value;

        Assert.Equal(48213, changes.LastChange);
        Assert.True(changes.Complete);
        Assert.Equal(new[] { "1d/BTCUSDT-1d-2026-11-03.csv", "15m/BTCUSDT-15m-2026-11-04.csv" }, changes.Files);
    }

    [Fact]
    public async Task Purged_changes_are_reported_as_incomplete()
    {
        _downloader.Answer("GET /api/v1/status/binance/BTCUSDT/changes?after=5", HttpStatusCode.OK, "changes-purged.json");

        var changes = (await _downloader.NewClient().GetChangesAsync("binance", "BTCUSDT", 5, CancellationToken.None)).Value;

        Assert.False(changes.Complete);
        Assert.Empty(changes.Files);
    }

    [Fact]
    public async Task A_timeout_a_network_error_and_an_unreadable_answer_are_results_not_exceptions()
    {
        var client = _downloader.NewClient();

        _downloader.Fail("GET /api/v1/status/binance/BTCUSDT", new TaskCanceledException("timeout"));
        var timeout = await client.GetSymbolStatusAsync("binance", "BTCUSDT", CancellationToken.None);

        _downloader.Fail("GET /api/v1/status/binance/BTCUSDT", new HttpRequestException("No route to host"));
        var network = await client.GetSymbolStatusAsync("binance", "BTCUSDT", CancellationToken.None);

        _downloader.AnswerText("GET /api/v1/status/binance/BTCUSDT", HttpStatusCode.OK, "<html>not json</html>");
        var unreadable = await client.GetSymbolStatusAsync("binance", "BTCUSDT", CancellationToken.None);

        _downloader.AnswerText("GET /api/v1/status/binance/BTCUSDT", HttpStatusCode.BadGateway, "<html>proxy</html>");
        var gateway = await client.GetSymbolStatusAsync("binance", "BTCUSDT", CancellationToken.None);

        Assert.Equal((ErrorType.Unavailable, "downloader.timeout"), (timeout.Error.Type, timeout.Error.Code));
        Assert.Equal((ErrorType.Unavailable, "downloader.unreachable"), (network.Error.Type, network.Error.Code));
        Assert.Equal((ErrorType.InvalidData, "downloader.invalidAnswer"), (unreadable.Error.Type, unreadable.Error.Code));
        Assert.Equal((ErrorType.Unavailable, "The Downloader answered 502."), (gateway.Error.Type, gateway.Error.Message));
    }

    [Fact]
    public async Task A_request_cancelled_by_the_caller_is_cancelled()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _downloader.Fail("GET /api/v1/status/binance/BTCUSDT", new OperationCanceledException(cancelled.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _downloader.NewClient().GetSymbolStatusAsync("binance", "BTCUSDT", cancelled.Token));
    }

    [Fact]
    public async Task A_Downloader_that_is_switched_off_is_never_called()
    {
        var client = _downloader.NewClient(enabled: false);

        var lookup = await client.LookupAsync("binance", "BTCUSDT", CancellationToken.None);
        var unsubscribe = await client.UnsubscribeAsync("binance", "BTCUSDT", CancellationToken.None);

        Assert.False(client.Enabled);
        Assert.Equal("downloader.disabled", lookup.Error.Code);
        Assert.Equal(ErrorType.Unavailable, unsubscribe.Error.Type);
        Assert.Empty(_downloader.Requests);
    }

    [Fact]
    public async Task Names_are_escaped_in_the_path()
    {
        _downloader.Answer("GET /api/v1/sources/yahoo/symbols/EURUSD%3DX", HttpStatusCode.OK, "lookup-BTCUSDT.json");

        var result = await _downloader.NewClient().LookupAsync("yahoo", "EURUSD=X", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "GET /api/v1/sources/yahoo/symbols/EURUSD%3DX" }, _downloader.Requests);
    }

    [Fact]
    public async Task The_service_status_carries_cycle_queue_sources_catalogs_and_the_check()
    {
        _downloader.Answer("GET /api/v1/status", HttpStatusCode.OK, "service-status.json");

        var status = (await _downloader.NewClient().GetServiceStatusAsync(CancellationToken.None)).Value;

        Assert.Equal(("running", "1.4.0"), (status.State, status.Version));
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 21, 5, 12, TimeSpan.Zero), status.StartedAt);
        Assert.Equal(new UpdateCycle(
            new DateTimeOffset(2026, 11, 18, 9, 30, 30, TimeSpan.Zero), new DateTimeOffset(2026, 11, 18, 9, 30, 41, TimeSpan.Zero),
            "partial", 26, "yahoo: rate limited, 4 jobs postponed"), status.LastCycle);
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 9, 45, 30, TimeSpan.Zero), status.NextCycleAt);
        Assert.Equal(new DownloaderQueue(0, 312, 2, 1, 0), status.Queue);
        Assert.Equal(new[] { "ok", "rateLimited" }, status.Sources.Select(source => source.State));
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 9, 40, 33, TimeSpan.Zero), status.Sources[1].RetryAt);
        Assert.Equal(TimeSpan.Zero, status.Sources[1].RetryAt!.Value.Offset); // UTC everywhere (MD7)
        Assert.Equal(new CatalogState("binance", new DateTimeOffset(2026, 11, 18, 4, 0, 2, TimeSpan.Zero), 3412, null, null), status.Catalogs[0]);
        Assert.Null(status.Catalogs[1].RefreshedAt);
        Assert.Equal(new ConsistencyCheck(new DateTimeOffset(2026, 11, 16, 3, 0, 5, TimeSpan.Zero), 0), status.ConsistencyCheck);
    }

    [Fact]
    public async Task A_Downloader_that_just_started_has_no_cycle_check_or_catalogs_yet()
    {
        _downloader.Answer("GET /api/v1/status", HttpStatusCode.OK, "service-status-first-start.json");

        var status = (await _downloader.NewClient().GetServiceStatusAsync(CancellationToken.None)).Value;

        Assert.Null(status.LastCycle);
        Assert.Null(status.NextCycleAt);
        Assert.Null(status.ConsistencyCheck);
        Assert.Empty(status.Sources);
        Assert.Empty(status.Catalogs); // an older Downloader without the field
    }

    [Fact]
    public async Task All_subscriptions_come_merged_with_their_clients_and_the_timeframes_TradeViewer_knows()
    {
        _downloader.Answer("GET /api/v1/subscriptions", HttpStatusCode.OK, "subscriptions-all.json");

        var subscriptions = (await _downloader.NewClient().GetSubscriptionsAsync(CancellationToken.None)).Value;

        Assert.Equal(new[] { "BTCUSDT", "BRK-B" }, subscriptions.Select(subscription => subscription.Symbol));
        Assert.Equal(new[] { "ewts", "tradeviewer-test" }, subscriptions[0].SubscribedBy);
        Assert.Equal(new[] { Timeframe.M15, Timeframe.D1 }, subscriptions[0].Timeframes.Select(timeframe => timeframe.Timeframe)); // 1M left out, smallest first
        Assert.Equal(new DateOnly(2026, 8, 18), subscriptions[0].Timeframes[0].WantedFrom);
        Assert.Null(subscriptions[0].Timeframes[1].WantedFrom);
        Assert.Equal("BRK.B", subscriptions[1].SourceSymbol);
    }

    [Fact]
    public async Task The_status_calls_fail_as_results_when_the_Downloader_is_away_or_switched_off()
    {
        _downloader.Fail("GET /api/v1/status", new HttpRequestException("Connection refused"));

        var away = await _downloader.NewClient().GetServiceStatusAsync(CancellationToken.None);
        var off = await _downloader.NewClient(enabled: false).GetSubscriptionsAsync(CancellationToken.None);

        Assert.Equal("downloader.unreachable", away.Error.Code);
        Assert.Equal("downloader.disabled", off.Error.Code);
        Assert.Equal(new[] { "GET /api/v1/status" }, _downloader.Requests); // switched off: nothing is asked
    }

    [Fact]
    public async Task The_symbol_status_says_when_each_timeframe_last_changed()
    {
        _downloader.Answer("GET /api/v1/status/binance/BTCUSDT", HttpStatusCode.OK, "status-BTCUSDT.json");

        var status = (await _downloader.NewClient().GetSymbolStatusAsync("binance", "BTCUSDT", CancellationToken.None)).Value;

        Assert.Equal(new DateTimeOffset(2026, 11, 4, 15, 30, 31, TimeSpan.Zero), status.Timeframes[0].LastChangedAt);
        Assert.Null(status.Timeframes[1].LastChangedAt);
    }
    [Fact]
    public async Task The_service_status_recorded_from_the_real_Downloader_is_read_completely()
    {
        // Recorded on 2026-11-18 from the Downloader on the Pi (read only): an old error of a source that is ok again.
        _downloader.Answer("GET /api/v1/status", HttpStatusCode.OK, "recorded-2026-11-18-service-status.json");

        var status = (await _downloader.NewClient().GetServiceStatusAsync(CancellationToken.None)).Value;

        Assert.Equal(("running", "0.1.0", "ok", 39), (status.State, status.Version, status.LastCycle!.Result, status.LastCycle.JobsPlanned));
        Assert.Equal(new[] { "binance", "tradingview", "yahoo" }, status.Sources.Select(source => source.Source));
        Assert.All(status.Sources, source => Assert.Equal("ok", source.State));
        Assert.StartsWith("Name or service not known", status.Sources[0].LastError);
        Assert.Equal(new[] { 1373, 6218, 12052 }, status.Catalogs.Select(catalog => catalog.SymbolCount));
        Assert.Equal(30, status.ConsistencyCheck!.Problems);
        Assert.Equal(DownloaderStatusRules.Green, DownloaderStatusRules.LightOf(true, true, status));
    }

    [Fact]
    public async Task The_subscriptions_recorded_from_the_real_Downloader_are_read_completely()
    {
        _downloader.Answer("GET /api/v1/subscriptions", HttpStatusCode.OK, "recorded-2026-11-18-subscriptions.json");

        var subscriptions = (await _downloader.NewClient().GetSubscriptionsAsync(CancellationToken.None)).Value;

        Assert.Equal(13, subscriptions.Count);
        Assert.All(subscriptions, subscription => Assert.Equal(5, subscription.Timeframes.Count));
        Assert.Equal(new[] { "ewts", "ewts-dev", "tradeviewer" }, subscriptions.Single(item => item.Symbol == "BTCUSDT").SubscribedBy);
    }

    [Fact]
    public async Task The_symbol_status_recorded_from_the_real_Downloader_has_its_confirmed_gaps()
    {
        _downloader.Answer("GET /api/v1/status/binance/BTCUSDT", HttpStatusCode.OK, "recorded-2026-11-18-status-BTCUSDT.json");

        var status = (await _downloader.NewClient().GetSymbolStatusAsync("binance", "BTCUSDT", CancellationToken.None)).Value;

        Assert.Equal(new[] { 21, 20, 4, 0, 0 }, status.Timeframes.Select(timeframe => timeframe.Gaps.Count));
        Assert.All(status.Timeframes.SelectMany(timeframe => timeframe.Gaps), gap => Assert.Equal(DownloaderStatusRules.MissingUpstream, gap.Reason));
    }
}
