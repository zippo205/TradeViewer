using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.Charts;
using TradeViewer.Contracts.Indicators;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>Indicators in /api/chart-settings and /api/indicator-templates through the real application (IN1, IN2, CP2).</summary>
public class IndicatorSettingsApiTests : IAsyncLifetime
{
    private const string Settings = "/api/chart-settings/binance/BTCUSDT";

    private readonly TradeViewerFactory _factory = new TradeViewerFactory();
    private readonly ConcurrentQueue<string> _events = new ConcurrentQueue<string>();
    private HttpClient _client = null!;
    private HubConnection _hub = null!;

    public async Task InitializeAsync()
    {
        var folder = Path.Combine(_factory.MarketDataRoot, "binance", "BTCUSDT", "15m");
        Directory.CreateDirectory(folder);
        File.Copy(Fixtures.CandleFile("binance", "BTCUSDT", "15m", "BTCUSDT-15m-2025-01-01.csv"), Path.Combine(folder, "BTCUSDT-15m-2025-01-01.csv"));

        _client = _factory.CreateClient();
        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(new Dictionary<string, int?> { ["15m"] = null }));
        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));

        _hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "hubs/workspace"), options => options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();
        _hub.On<string, string, string>("ChartSettingsChanged", (source, symbol, window) => _events.Enqueue($"ChartSettingsChanged {source}/{symbol} {window}"));
        await _hub.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _hub.DisposeAsync();
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Indicators_are_saved_with_the_chart_settings_and_the_other_windows_hear_of_it()
    {
        var bb = Bollinger();
        var request = new HttpRequestMessage(HttpMethod.Put, Settings) { Content = JsonContent.Create(new ChartSettingsResponse(false, new[] { bb })) };
        request.Headers.Add("X-TradeViewer-Window", "window-1");

        var put = await _client.SendAsync(request);
        var json = await _client.GetStringAsync(Settings);
        var read = JsonSerializer.Deserialize<ChartSettingsResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Contains("\"indicators\":[{\"id\":\"" + bb.Id + "\",\"type\":\"bb\",\"length\":20,\"deviation\":2,\"fast\":null,", json);
        Assert.Equal(bb.Styles, Assert.Single(read.Indicators!).Styles);
        await WaitForAsync("ChartSettingsChanged binance/BTCUSDT window-1");
    }

    [Fact]
    public async Task A_client_that_sends_no_indicators_keeps_them()
    {
        await _client.PutAsJsonAsync(Settings, new ChartSettingsResponse(false, new[] { Bollinger() }));

        var put = await _client.PutAsync(Settings, JsonContent.Create(new { drawingsHidden = true })); // as the build before this epic sent it
        var read = await _client.GetFromJsonAsync<ChartSettingsResponse>(Settings);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.True(read!.DrawingsHidden);
        Assert.Single(read.Indicators!);
    }

    [Fact]
    public async Task A_wrong_indicator_is_a_problem_naming_its_field()
    {
        var put = await _client.PutAsJsonAsync(Settings, new ChartSettingsResponse(false, new[] { Bollinger() with { Deviation = 0 } }));

        var problem = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("indicators[0].deviation", out _));
    }

    [Fact]
    public async Task Templates_are_saved_listed_and_deleted()
    {
        var trend = new IndicatorTemplateRecord(Guid.NewGuid(), "Trend", new[] { Bollinger() });

        var put = await _client.PutAsJsonAsync("/api/indicator-templates/" + trend.Id, trend);
        var taken = await _client.PutAsJsonAsync("/api/indicator-templates/" + Guid.NewGuid(), trend with { Id = Guid.Empty });
        var sameName = Guid.NewGuid();
        var conflict = await _client.PutAsJsonAsync("/api/indicator-templates/" + sameName, trend with { Id = sameName, Name = "trend" });
        var list = await _client.GetFromJsonAsync<List<IndicatorTemplateRecord>>("/api/indicator-templates");
        var delete = await _client.DeleteAsync("/api/indicator-templates/" + trend.Id);
        var again = await _client.DeleteAsync("/api/indicator-templates/" + trend.Id);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode); // the id of the body differs from the address
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("Trend", Assert.Single(list!).Name);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    private static IndicatorRecord Bollinger()
    {
        var styles = new[]
        {
            new IndicatorStyleRecord("basis", "#2962FF", 1),
            new IndicatorStyleRecord("upper", "#F23645", 2),
            new IndicatorStyleRecord("lower", "#089981", 1)
        };
        return new IndicatorRecord(Guid.NewGuid(), "bb", 20, 2, null, null, null, "close", styles, false);
    }

    private async Task WaitForAsync(string expected)
    {
        for (var attempt = 0; attempt < 250 && !_events.Contains(expected); attempt++)
        {
            await Task.Delay(20);
        }

        Assert.Contains(expected, _events);
    }
}
