using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.Charts;
using TradeViewer.Contracts.Drawings;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>
/// /api/drawings, /api/fibonacci-presets and /api/chart-settings through the real application (DR1-DR6, CP2,
/// WS1), and the hub event that lets other windows follow.
/// </summary>
public class DrawingsApiTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Time = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly TradeViewerFactory _factory = new TradeViewerFactory();
    private readonly ConcurrentQueue<string> _events = new ConcurrentQueue<string>();
    private HttpClient _client = null!;
    private HubConnection _hub = null!;

    public async Task InitializeAsync()
    {
        // An asset can be added only when the output root has its files: the fixture day of BTCUSDT under both names.
        foreach (var symbol in new[] { "BTCUSDT", "ETHUSDT" })
        {
            var folder = Path.Combine(_factory.MarketDataRoot, "binance", symbol, "15m");
            Directory.CreateDirectory(folder);
            File.Copy(Fixtures.CandleFile("binance", "BTCUSDT", "15m", "BTCUSDT-15m-2025-01-01.csv"), Path.Combine(folder, symbol + "-15m-2025-01-01.csv"));
        }

        _client = _factory.CreateClient();
        (await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"))).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "ETHUSDT"))).EnsureSuccessStatusCode();

        _hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "hubs/workspace"), options => options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();
        _hub.On<string, string, string>("DrawingsChanged", (source, symbol, window) => _events.Enqueue(source + "/" + symbol + " from " + window));
        await _hub.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _hub.DisposeAsync();
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task A_drawing_is_saved_read_back_changed_and_deleted_and_survives_a_restart()
    {
        var line = TrendLine("BTCUSDT");

        var put = await PutAsync(line, "window-a");
        var again = await PutAsync(line, "window-a");
        var moved = line with { Anchors = new[] { Anchor(2, 101.5), Anchor(30, 140) }, Text = "neckline" };
        await PutAsync(moved, "window-a");

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(Json(new[] { moved }), Json(await GetAsync("BTCUSDT")));
        Assert.Empty(await GetAsync("ETHUSDT"));

        using (var restarted = _factory.Restart())
        {
            using var client = restarted.CreateClient();
            var read = await client.GetFromJsonAsync<List<DrawingRecord>>("/api/drawings/binance/BTCUSDT");
            Assert.Equal(Json(new[] { moved }), Json(read));

            var delete = await client.DeleteAsync("/api/drawings/" + line.Id);
            var deleteAgain = await client.DeleteAsync("/api/drawings/" + line.Id);
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
            Assert.Empty((await client.GetFromJsonAsync<List<DrawingRecord>>("/api/drawings/binance/BTCUSDT"))!);
        }
    }

    [Fact]
    public async Task Times_are_written_in_UTC_and_anchors_after_the_last_candle_are_kept()
    {
        var future = TrendLine("BTCUSDT") with { Anchors = new[] { Anchor(0, 100), Anchor(24 * 400, 250_000) } };
        await PutAsync(future, null);

        var json = await _client.GetStringAsync("/api/drawings/binance/BTCUSDT");

        Assert.Contains("\"time\":\"2026-11-15T10:00:00+00:00\"", json);
        Assert.Contains("\"time\":\"2027-12-20T10:00:00+00:00\"", json);
    }

    [Fact]
    public async Task An_invalid_drawing_is_answered_with_the_fields_and_not_stored()
    {
        var line = TrendLine("BTCUSDT") with { Style = new DrawingStyleRecord("blue", 7, "solid") };

        var response = await PutAsync(line, null);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("style.color", out _));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("style.width", out _));
        Assert.Empty(await GetAsync("BTCUSDT"));
    }

    [Fact]
    public async Task Unknown_assets_and_drawings_of_another_asset_are_refused()
    {
        var line = TrendLine("BTCUSDT");
        await PutAsync(line, null);

        var unknownAsset = await PutAsync(TrendLine("NOPEUSDT"), null);
        var otherAsset = await PutAsync(line with { Symbol = "ETHUSDT" }, null);
        var otherId = await _client.PutAsJsonAsync("/api/drawings/" + Guid.NewGuid(), line);
        var listUnknown = await _client.GetAsync("/api/drawings/binance/NOPEUSDT");

        Assert.Equal(HttpStatusCode.NotFound, unknownAsset.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, otherAsset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, otherId.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, listUnknown.StatusCode);
    }

    [Fact]
    public async Task Other_windows_hear_which_asset_changed_and_which_window_changed_it()
    {
        var line = TrendLine("ETHUSDT");

        await PutAsync(line, "window-a");
        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/drawings/" + line.Id);
        delete.Headers.Add("X-TradeViewer-Window", "window-b");
        await _client.SendAsync(delete);

        await WaitForAsync("binance/ETHUSDT from window-a");
        await WaitForAsync("binance/ETHUSDT from window-b");
        Assert.Equal(2, _events.Count);
    }

    [Fact]
    public async Task Removing_an_asset_deletes_its_drawings()
    {
        await PutAsync(TrendLine("BTCUSDT"), null);

        await _client.DeleteAsync("/api/assets/binance/BTCUSDT");
        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));

        Assert.Empty(await GetAsync("BTCUSDT"));
    }

    [Fact]
    public async Task Presets_start_with_the_defaults_can_be_added_and_the_last_of_a_kind_stays()
    {
        var defaults = await _client.GetFromJsonAsync<List<FibonacciPresetRecord>>("/api/fibonacci-presets");
        var id = Guid.NewGuid();
        var put = await _client.PutAsJsonAsync("/api/fibonacci-presets/" + id, new FibonacciPresetRecord(id, "Golden", "extension", new[] { 1.618, 1 }));
        var saved = await put.Content.ReadFromJsonAsync<FibonacciPresetRecord>();
        var invalid = await _client.PutAsJsonAsync("/api/fibonacci-presets/" + id, new FibonacciPresetRecord(id, "", "extension", new[] { 1.0 }));
        var retracement = defaults!.Single(preset => preset.Kind == "retracement");
        var deleteLast = await _client.DeleteAsync("/api/fibonacci-presets/" + retracement.Id);
        var deleteMine = await _client.DeleteAsync("/api/fibonacci-presets/" + id);

        Assert.Equal(new[] { "Retracement", "Extension" }, defaults!.Select(preset => preset.Name));
        Assert.Equal(new[] { 0, 0.236, 0.382, 0.5, 0.618, 0.786, 1 }, retracement.Levels);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(new[] { 1, 1.618 }, saved!.Levels);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteLast.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteMine.StatusCode);
        Assert.Equal(2, (await _client.GetFromJsonAsync<List<FibonacciPresetRecord>>("/api/fibonacci-presets"))!.Count);
    }

    [Fact]
    public async Task Chart_settings_are_kept_per_asset()
    {
        var before = await _client.GetFromJsonAsync<ChartSettingsResponse>("/api/chart-settings/binance/BTCUSDT");
        var put = await _client.PutAsJsonAsync("/api/chart-settings/binance/BTCUSDT", new ChartSettingsResponse(true));
        var after = await _client.GetFromJsonAsync<ChartSettingsResponse>("/api/chart-settings/binance/BTCUSDT");
        var other = await _client.GetFromJsonAsync<ChartSettingsResponse>("/api/chart-settings/binance/ETHUSDT");
        var unknown = await _client.PutAsJsonAsync("/api/chart-settings/binance/NOPEUSDT", new ChartSettingsResponse(true));

        Assert.False(before!.DrawingsHidden);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.True(after!.DrawingsHidden);
        Assert.False(other!.DrawingsHidden);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    private static AnchorRecord Anchor(int hours, double price)
    {
        return new AnchorRecord(Time.AddHours(hours), price);
    }

    private static DrawingRecord TrendLine(string symbol)
    {
        return new DrawingRecord(Guid.NewGuid(), "binance", symbol, "trendLine", 1, false, new[] { Anchor(0, 100), Anchor(12, 120.25) }, new DrawingStyleRecord("#2962FF", 2, "solid"));
    }

    private static string Json<T>(T value)
    {
        return JsonSerializer.Serialize(value);
    }

    private async Task<HttpResponseMessage> PutAsync(DrawingRecord drawing, string? window)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/drawings/" + drawing.Id) { Content = JsonContent.Create(drawing) };
        if (window != null)
        {
            request.Headers.Add("X-TradeViewer-Window", window);
        }

        return await _client.SendAsync(request);
    }

    private async Task<List<DrawingRecord>> GetAsync(string symbol)
    {
        return (await _client.GetFromJsonAsync<List<DrawingRecord>>("/api/drawings/binance/" + symbol))!;
    }

    private async Task WaitForAsync(string expected)
    {
        for (var attempt = 0; attempt < 500 && !_events.Contains(expected); attempt++)
        {
            await Task.Delay(20);
        }

        Assert.Contains(expected, _events);
    }
}
