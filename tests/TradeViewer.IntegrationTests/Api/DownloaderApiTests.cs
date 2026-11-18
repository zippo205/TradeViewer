using System.Net;
using System.Net.Http.Json;
using TradeViewer.Contracts.Downloader;
using TradeViewer.IntegrationTests.MarketData;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>
/// /api/downloader through the real application (DL1-DL4), the Downloader answered by the stub with recorded answers.
/// The page only reads: no request of these tests changes anything at the Downloader.
/// </summary>
public class DownloaderApiTests : IDisposable
{
    private readonly StubDownloader _downloader = new StubDownloader();
    private readonly TradeViewerFactory _factory;
    private readonly HttpClient _client;

    public DownloaderApiTests()
    {
        _factory = TradeViewerFactory.WithDownloader(_downloader);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task The_status_shows_the_Downloader_and_a_partial_cycle_with_a_rate_limited_source_is_yellow()
    {
        _downloader.Answer("GET /api/v1/status", HttpStatusCode.OK, "service-status.json");

        var json = await _client.GetStringAsync("/api/downloader/status");
        var status = await _client.GetFromJsonAsync<DownloaderStatusResponse>("/api/downloader/status");

        Assert.Contains("\"light\":\"yellow\"", json);
        Assert.Contains("\"lastCycle\":{\"startedAt\":\"2026-11-18T09:30:30+00:00\"", json);
        Assert.True(status!.Reachable);
        Assert.Equal("partial", status.Service!.LastCycle!.Result);
        Assert.Equal(new DownloaderQueueResponse(0, 312, 2, 1, 0), status.Service.Queue);
        Assert.Equal(new[] { "binance", "yahoo" }, status.Service.Catalogs.Select(catalog => catalog.Source));
        Assert.DoesNotContain(_downloader.Requests, request => !request.StartsWith("GET ", StringComparison.Ordinal) && request.Contains("/status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Downloader_that_does_not_answer_is_red_with_200_and_since_when()
    {
        _downloader.Fail("GET /api/v1/status", new HttpRequestException("Connection refused"));

        var response = await _client.GetAsync("/api/downloader/status");
        var status = await response.Content.ReadFromJsonAsync<DownloaderStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(status!.Reachable);
        Assert.Equal("red", status.Light);
        Assert.NotNull(status.UnreachableSince);
        Assert.Contains("Connection refused", status.Problem);
        Assert.Null(status.Service);
    }

    [Fact]
    public async Task All_subscriptions_come_with_TradeViewer_marked()
    {
        _downloader.Answer("GET /api/v1/subscriptions", HttpStatusCode.OK, "subscriptions-all.json");

        var result = await _client.GetFromJsonAsync<DownloaderSubscriptionsResponse>("/api/downloader/subscriptions");

        Assert.Equal("tradeviewer-test", result!.OwnClient);
        Assert.Equal(new[] { ("BTCUSDT", true), ("BRK-B", false) }, result.Subscriptions.Select(s => (s.Symbol, s.Own)));
        Assert.Equal(new[] { "15m", "1d" }, result.Subscriptions[0].Timeframes.Select(t => t.Timeframe));
    }

    [Fact]
    public async Task Unreachable_subscriptions_are_503_and_an_unknown_asset_is_404()
    {
        _downloader.Fail("GET /api/v1/subscriptions", new HttpRequestException("Connection refused"));

        var subscriptions = await _client.GetAsync("/api/downloader/subscriptions");
        var asset = await _client.GetAsync("/api/downloader/assets/binance/NOPEUSDT");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, subscriptions.StatusCode);
        Assert.Equal("application/problem+json", subscriptions.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, asset.StatusCode);
    }

    [Fact]
    public async Task Switched_off_the_status_says_so_and_nothing_is_asked()
    {
        using var factory = new TradeViewerFactory();
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<DownloaderStatusResponse>("/api/downloader/status");
        var subscriptions = await client.GetAsync("/api/downloader/subscriptions");

        Assert.False(status!.Enabled);
        Assert.Equal("off", status.Light);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, subscriptions.StatusCode);
    }
}
