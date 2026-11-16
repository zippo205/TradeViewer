using System.Net;
using System.Net.Http.Json;
using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.Charts;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>The chart settings per asset (CP2) and the last chart (CP3) through the real application.</summary>
public class ChartSettingsApiTests : IAsyncLifetime
{
    private const string Settings = "/api/chart-settings/binance/BTCUSDT";

    private readonly TradeViewerFactory _factory = new TradeViewerFactory();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var folder = Path.Combine(_factory.MarketDataRoot, "binance", "BTCUSDT", "1w");
        Directory.CreateDirectory(folder);
        File.Copy(Fixtures.CandleFile("binance", "BTCUSDT", "1w", "BTCUSDT-1w-2026-10.csv"), Path.Combine(folder, "BTCUSDT-1w-2026-10.csv"));

        _client = _factory.CreateClient();
        (await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"))).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_chart_starts_with_the_default_settings()
    {
        var settings = await _client.GetFromJsonAsync<ChartSettingsResponse>(Settings);

        Assert.Equal(new ChartSettingsResponse("linear"), settings);
    }

    [Fact]
    public async Task Changed_settings_are_saved_per_asset_and_survive_a_restart()
    {
        var put = await _client.PutAsJsonAsync(Settings, new ChartSettingsResponse("log"));
        var saved = await _client.GetFromJsonAsync<ChartSettingsResponse>(Settings);

        using var restarted = _factory.Restart();
        using var client = restarted.CreateClient();
        var afterRestart = await client.GetFromJsonAsync<ChartSettingsResponse>(Settings);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("log", saved!.PriceScale);
        Assert.Equal("log", afterRestart!.PriceScale);
    }

    [Fact]
    public async Task Wrong_settings_and_an_unknown_asset_are_refused()
    {
        var wrong = await _client.PutAsJsonAsync(Settings, new ChartSettingsResponse("cubic"));
        var unknownGet = await _client.GetAsync("/api/chart-settings/binance/ETHUSDT");
        var unknownPut = await _client.PutAsJsonAsync("/api/chart-settings/binance/ETHUSDT", new ChartSettingsResponse("log"));

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownGet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownPut.StatusCode);
        Assert.Equal("linear", (await _client.GetFromJsonAsync<ChartSettingsResponse>(Settings))!.PriceScale);
    }

    [Fact]
    public async Task Removing_an_asset_deletes_its_chart_settings()
    {
        await _client.PutAsJsonAsync(Settings, new ChartSettingsResponse("log"));

        (await _client.DeleteAsync("/api/assets/binance/BTCUSDT")).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"))).EnsureSuccessStatusCode();

        Assert.Equal("linear", (await _client.GetFromJsonAsync<ChartSettingsResponse>(Settings))!.PriceScale);
    }

    [Fact]
    public async Task The_last_chart_is_remembered_and_the_default_history_is_left_alone()
    {
        var before = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");

        var put = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, new LastChartResponse("binance", "BTCUSDT", "4h")));
        var after = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");

        Assert.Null(before!.LastChart);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(new LastChartResponse("binance", "BTCUSDT", "4h"), after!.LastChart);
        Assert.Equal(90, after.DefaultHistory!["15m"]);
    }

    [Fact]
    public async Task Saving_the_default_history_keeps_the_last_chart()
    {
        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, new LastChartResponse("binance", "BTCUSDT", "1d")));

        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(new Dictionary<string, int?> { ["15m"] = 30 }));
        var after = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");

        Assert.Equal(30, after!.DefaultHistory!["15m"]);
        Assert.Equal(new LastChartResponse("binance", "BTCUSDT", "1d"), after.LastChart);
    }

    [Fact]
    public async Task Settings_without_content_or_with_a_wrong_last_chart_are_refused_and_change_nothing()
    {
        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, new LastChartResponse("binance", "BTCUSDT", "1d")));

        var empty = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null));
        var wrongTimeframe = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, new LastChartResponse("binance", "BTCUSDT", "7m")));
        var halfWrong = await _client.PutAsJsonAsync(
            "/api/settings",
            new SettingsResponse(new Dictionary<string, int?> { ["15m"] = 10 }, new LastChartResponse("", "BTCUSDT", "1h")));
        var after = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongTimeframe.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, halfWrong.StatusCode);
        Assert.Equal(new LastChartResponse("binance", "BTCUSDT", "1d"), after!.LastChart);
        Assert.Equal(90, after.DefaultHistory!["15m"]); // the valid half of a wrong request was not written either
    }
}
