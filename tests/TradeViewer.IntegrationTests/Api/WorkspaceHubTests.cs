using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>The hub /hubs/workspace: an open window hears what changed without asking (MD2, AS4).</summary>
public class WorkspaceHubTests : IAsyncLifetime
{
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
        _hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "hubs/workspace"), options => options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        _hub.On<string, string, string[]>("CandlesUpdated", (source, symbol, timeframes) =>
            _events.Enqueue("CandlesUpdated " + source + "/" + symbol + " " + string.Join(",", timeframes)));
        _hub.On<string, string>("AssetStatusChanged", (source, symbol) => _events.Enqueue("AssetStatusChanged " + source + "/" + symbol));

        await _hub.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _hub.DisposeAsync();
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Adding_an_asset_tells_the_open_windows_about_its_progress_and_its_new_candles()
    {
        var complete = new Dictionary<string, int?> { ["15m"] = null, ["1h"] = null, ["4h"] = null, ["1d"] = null, ["1w"] = null };
        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(complete));

        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));
        await WaitForAsync("CandlesUpdated binance/BTCUSDT 15m");

        Assert.Contains("AssetStatusChanged binance/BTCUSDT", _events);
    }

    [Fact]
    public async Task Changed_files_reach_the_open_windows_after_a_reload()
    {
        var complete = new Dictionary<string, int?> { ["15m"] = null };
        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(complete));
        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));
        await WaitForAsync("CandlesUpdated binance/BTCUSDT 15m");
        _events.Clear();

        // The Downloader writes the next day; "reload" stands for the sync here (the Downloader is off in tests).
        var folder = Path.Combine(_factory.MarketDataRoot, "binance", "BTCUSDT", "15m");
        File.Copy(Fixtures.CandleFile("binance", "BTCUSDT", "15m", "BTCUSDT-15m-2024-12-31.csv"), Path.Combine(folder, "BTCUSDT-15m-2024-12-31.csv"));
        await _client.PostAsync("/api/assets/binance/BTCUSDT/rescan", null);

        await WaitForAsync("CandlesUpdated binance/BTCUSDT 15m");
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
