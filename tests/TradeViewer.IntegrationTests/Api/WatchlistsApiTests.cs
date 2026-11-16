using System.Net;
using System.Net.Http.Json;
using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.Watchlists;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>/api/watchlists through the real application: rows with figures from the candle store, and the order (AS1, AS6).</summary>
public class WatchlistsApiTests : IAsyncLifetime
{
    private static readonly string[] Symbols = { "BTCUSDT", "ETHUSDT", "SOLUSDT" };

    private readonly TradeViewerFactory _factory = new TradeViewerFactory();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        // Each symbol: two days of 15m candles and the daily candle of the first day (the fixture files of BTCUSDT under its name).
        foreach (var symbol in Symbols)
        {
            Copy(symbol, "15m", "BTCUSDT-15m-2024-12-31.csv");
            Copy(symbol, "15m", "BTCUSDT-15m-2025-01-01.csv");
            Write(symbol, "1d", symbol + "-1d-2024-12-31.csv", "1735603200000,92792.05,95000,89000,90000.00,1000");
        }

        _client = _factory.CreateClient();
        var complete = new Dictionary<string, int?> { ["15m"] = null, ["1h"] = null, ["4h"] = null, ["1d"] = null, ["1w"] = null };
        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(complete));
        foreach (var symbol in Symbols)
        {
            (await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", symbol))).EnsureSuccessStatusCode();
        }

        for (var attempt = 0; attempt < 500; attempt++)
        {
            var assets = await _client.GetFromJsonAsync<List<AssetResponse>>("/api/assets");
            if (assets!.All(asset => !asset.Loading && asset.Timeframes[0].Chartable))
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("The assets did not finish loading.");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_five_lists_come_with_rows_and_figures_from_the_candle_store()
    {
        var lists = await _client.GetFromJsonAsync<List<WatchlistResponse>>("/api/watchlists");

        Assert.Equal(new[] { "crypto", "stock", "etf", "forex", "commodity" }, lists!.Select(list => list.AssetClass));
        Assert.Equal(Symbols, lists![0].Rows.Select(row => row.Symbol));

        var row = lists[0].Rows[0];
        Assert.Equal("BTC/USDT", row.DisplayName);
        Assert.Equal(new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero), row.LastCloseTime); // the candle of 23:45 closed at midnight
        Assert.NotNull(row.LastClose);
        Assert.Equal((row.LastClose!.Value - 90000) / 90000 * 100, row.ChangePercent!.Value, 8);  // against the close of 2024-12-31
        Assert.Equal(new[] { "15m", "1d" }, row.Timeframes);
        Assert.False(row.Loading);
        Assert.Null(row.Warning);
    }

    [Fact]
    public async Task A_new_order_is_saved_and_survives_a_restart()
    {
        var order = new ReorderWatchlistRequest(new[] { Entry("SOLUSDT"), Entry("BTCUSDT"), Entry("ETHUSDT") });

        var put = await _client.PutAsJsonAsync("/api/watchlists/crypto/order", order);
        var after = await _client.GetFromJsonAsync<List<WatchlistResponse>>("/api/watchlists");

        using var restarted = _factory.Restart();
        using var client = restarted.CreateClient();
        var afterRestart = await client.GetFromJsonAsync<List<WatchlistResponse>>("/api/watchlists");
        var assets = await client.GetFromJsonAsync<List<AssetResponse>>("/api/assets");

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal(new[] { "SOLUSDT", "BTCUSDT", "ETHUSDT" }, after![0].Rows.Select(row => row.Symbol));
        Assert.Equal(new[] { "SOLUSDT", "BTCUSDT", "ETHUSDT" }, afterRestart![0].Rows.Select(row => row.Symbol));
        Assert.Equal(new[] { "SOLUSDT", "BTCUSDT", "ETHUSDT" }, assets!.Select(asset => asset.Symbol)); // the assets page follows the same order
    }

    [Fact]
    public async Task Wrong_orders_are_refused_with_problem_details_and_change_nothing()
    {
        var incomplete = await _client.PutAsJsonAsync("/api/watchlists/crypto/order", new ReorderWatchlistRequest(new[] { Entry("ETHUSDT"), Entry("BTCUSDT") }));
        var unknownList = await _client.PutAsJsonAsync("/api/watchlists/bonds/order", new ReorderWatchlistRequest(Array.Empty<WatchlistEntry>()));
        var noAssets = await _client.PutAsJsonAsync("/api/watchlists/crypto/order", new { });
        var lists = await _client.GetFromJsonAsync<List<WatchlistResponse>>("/api/watchlists");

        Assert.Equal(HttpStatusCode.Conflict, incomplete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownList.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noAssets.StatusCode);
        Assert.All(new[] { incomplete, unknownList, noAssets }, response => Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType));
        Assert.Equal(Symbols, lists![0].Rows.Select(row => row.Symbol));
    }

    [Fact]
    public async Task A_new_asset_goes_to_the_end_of_its_list_and_a_removed_one_leaves_it()
    {
        await _client.PutAsJsonAsync("/api/watchlists/crypto/order", new ReorderWatchlistRequest(new[] { Entry("SOLUSDT"), Entry("ETHUSDT"), Entry("BTCUSDT") }));
        Copy("XRPUSDT", "15m", "BTCUSDT-15m-2025-01-01.csv");

        (await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "XRPUSDT"))).EnsureSuccessStatusCode();
        (await _client.DeleteAsync("/api/assets/binance/ETHUSDT")).EnsureSuccessStatusCode();
        var lists = await _client.GetFromJsonAsync<List<WatchlistResponse>>("/api/watchlists");

        Assert.Equal(new[] { "SOLUSDT", "BTCUSDT", "XRPUSDT" }, lists![0].Rows.Select(row => row.Symbol));
    }

    private static WatchlistEntry Entry(string symbol)
    {
        return new WatchlistEntry("binance", symbol);
    }

    private void Copy(string symbol, string timeframe, string fixture)
    {
        var lines = File.ReadAllLines(Fixtures.CandleFile("binance", "BTCUSDT", timeframe, fixture));
        Write(symbol, timeframe, fixture.Replace("BTCUSDT", symbol), lines);
    }

    private void Write(string symbol, string timeframe, string name, params string[] lines)
    {
        var folder = Path.Combine(_factory.MarketDataRoot, "binance", symbol, timeframe);
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, name), lines);
    }
}
