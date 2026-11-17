using System.Net;
using System.Net.Http.Json;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>/api/settings through the real application: the last chart (CP3) and the price scale mode of all charts (CP2).</summary>
public class SettingsApiTests : IDisposable
{
    private readonly TradeViewerFactory _factory = new TradeViewerFactory();
    private readonly HttpClient _client;

    public SettingsApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task At_first_there_is_no_last_chart_and_the_price_scale_is_linear()
    {
        var settings = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");

        Assert.Null(settings!.LastChart);
        Assert.Equal("linear", settings.PriceScale);
        Assert.Equal(90, settings.DefaultHistory!["15m"]);
    }

    [Fact]
    public async Task Each_part_is_saved_on_its_own_and_leaves_the_others_alone()
    {
        var chart = new LastChartResponse("binance", "BTCUSDT", "4h");

        var putChart = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, chart));
        var putScale = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, null, "log"));
        var putHistory = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(new Dictionary<string, int?> { ["15m"] = 30 }));
        var after = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");

        Assert.All(new[] { putChart, putScale, putHistory }, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(chart, after!.LastChart);
        Assert.Equal("log", after.PriceScale);
        Assert.Equal(30, after.DefaultHistory!["15m"]);
    }

    [Fact]
    public async Task The_price_scale_mode_survives_a_restart_and_can_be_set_back()
    {
        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, null, "log"));

        using var restarted = _factory.Restart();
        using var client = restarted.CreateClient();
        var afterRestart = await client.GetFromJsonAsync<SettingsResponse>("/api/settings");
        await client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, null, "linear"));
        var back = await client.GetFromJsonAsync<SettingsResponse>("/api/settings");

        Assert.Equal("log", afterRestart!.PriceScale);
        Assert.Equal("linear", back!.PriceScale);
    }

    [Fact]
    public async Task The_magnet_is_on_at_first_and_kept_when_switched_off()
    {
        var first = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");
        var put = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, null, null, false));

        using var restarted = _factory.Restart();
        using var client = restarted.CreateClient();
        var afterRestart = await client.GetFromJsonAsync<SettingsResponse>("/api/settings");

        Assert.True(first!.Magnet);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.False(afterRestart!.Magnet);
        Assert.Equal("linear", afterRestart.PriceScale); // the other parts stay
    }

    [Fact]
    public async Task Settings_without_content_or_with_a_wrong_part_are_refused_and_change_nothing()
    {
        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, new LastChartResponse("binance", "BTCUSDT", "1d"), "log"));

        var empty = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null));
        var wrongTimeframe = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, new LastChartResponse("binance", "BTCUSDT", "7m")));
        var wrongScale = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(null, null, "cubic"));
        var halfWrong = await _client.PutAsJsonAsync(
            "/api/settings",
            new SettingsResponse(new Dictionary<string, int?> { ["15m"] = 10 }, new LastChartResponse("", "BTCUSDT", "1h"), "linear"));
        var after = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");

        Assert.All(new[] { empty, wrongTimeframe, wrongScale, halfWrong }, response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));
        Assert.Equal(new LastChartResponse("binance", "BTCUSDT", "1d"), after!.LastChart);
        Assert.Equal("log", after.PriceScale);          // the valid parts of a wrong request were not written either
        Assert.Equal(90, after.DefaultHistory!["15m"]);
    }
}
