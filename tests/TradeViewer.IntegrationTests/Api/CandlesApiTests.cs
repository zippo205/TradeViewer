using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.MarketData;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>/api/candles through the real application: files of the Downloader in, column arrays out (MD1, MD8).</summary>
public class CandlesApiTests : IAsyncLifetime
{
    private readonly TradeViewerFactory _factory = new TradeViewerFactory();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        Copy("15m", "BTCUSDT-15m-2024-12-31.csv");
        Copy("15m", "BTCUSDT-15m-2025-01-01.csv");

        // The Downloader has delivered the hours of 2024-12-31 only: 2025-01-01 00:00 to 23:45 is "in progress" for 1d,
        // and the last hour of the 15m data is the current 1h candle.
        var hours = File.ReadAllLines(Fixtures.CandleFile("binance", "BTCUSDT", "15m", "BTCUSDT-15m-2024-12-31.csv"))
            .Where((_, index) => index % 4 == 0);
        Write("1h", "BTCUSDT-1h-2024-12-31.csv", hours);

        _client = _factory.CreateClient();
        var complete = new Dictionary<string, int?> { ["15m"] = null, ["1h"] = null, ["4h"] = null, ["1d"] = null, ["1w"] = null };
        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(complete));
        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));

        for (var attempt = 0; attempt < 500; attempt++)
        {
            var asset = await _client.GetFromJsonAsync<AssetResponse>("/api/assets/binance/BTCUSDT");
            if (!asset!.Loading && asset.Timeframes[0].Chartable)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("The asset did not finish loading.");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Candles_come_as_column_arrays_with_the_values_of_the_file()
    {
        var response = await _client.GetAsync("/api/candles/binance/BTCUSDT/15m");
        var json = await response.Content.ReadAsStringAsync();
        var page = JsonSerializer.Deserialize<CandlesResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("{\"t\":[1735603200,1735604100,", json);
        Assert.Contains("\"partial\":false", json);
        Assert.Equal(192, page.T.Length);
        Assert.Equal(new[] { 92792.05, 92907.03, 92706.02, 92831.10, 148.07186 }, new[] { page.O[0], page.H[0], page.L[0], page.C[0], page.V[0] });
        Assert.Equal(93576.00, page.O[96]); // the first candle of the file in microseconds
    }

    [Fact]
    public async Task Scrolling_back_reads_the_page_before_the_oldest_candle()
    {
        var latest = await _client.GetFromJsonAsync<CandlesResponse>("/api/candles/binance/BTCUSDT/15m?limit=100");
        var older = await _client.GetFromJsonAsync<CandlesResponse>("/api/candles/binance/BTCUSDT/15m?limit=100&to=" + latest!.T[0]);
        var none = await _client.GetFromJsonAsync<CandlesResponse>("/api/candles/binance/BTCUSDT/15m?to=" + older!.T[0]);

        Assert.Equal(100, latest.T.Length);
        Assert.Equal(92, older.T.Length);
        Assert.Equal(1735603200, older.T[0]);
        Assert.Equal(latest.T[0] - 900, older.T[91]);
        Assert.Empty(none!.T);
    }

    [Fact]
    public async Task A_larger_timeframe_ends_with_the_current_candle_marked_as_partial()
    {
        var hours = await _client.GetFromJsonAsync<CandlesResponse>("/api/candles/binance/BTCUSDT/1h");
        var quarters = await _client.GetFromJsonAsync<CandlesResponse>("/api/candles/binance/BTCUSDT/15m?limit=4");

        Assert.True(hours!.Partial);
        Assert.Equal(25, hours.T.Length);                               // 24 closed hours and the current one
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 23, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), hours.T[24]);
        Assert.Equal(quarters!.O[0], hours.O[24]);
        Assert.Equal(quarters.C[3], hours.C[24]);
        Assert.Equal(quarters.H.Max(), hours.H[24]);
        Assert.Equal(quarters.L.Min(), hours.L[24]);
        Assert.Equal(quarters.V.Sum(), hours.V[24], 8);
    }

    [Fact]
    public async Task Wrong_requests_are_answered_with_problem_details()
    {
        var unknownAsset = await _client.GetAsync("/api/candles/binance/ETHUSDT/15m");
        var noTimeframe = await _client.GetAsync("/api/candles/binance/BTCUSDT/7m");
        var tooMany = await _client.GetAsync("/api/candles/binance/BTCUSDT/15m?limit=5001");
        var notANumber = await _client.GetAsync("/api/candles/binance/BTCUSDT/15m?to=yesterday");

        Assert.Equal(HttpStatusCode.NotFound, unknownAsset.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noTimeframe.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notANumber.StatusCode);
        Assert.All(
            new[] { unknownAsset, noTimeframe, tooMany, notANumber },
            response => Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType));
    }

    private void Copy(string timeframe, string name)
    {
        Write(timeframe, name, File.ReadAllLines(Fixtures.CandleFile("binance", "BTCUSDT", timeframe, name)));
    }

    private void Write(string timeframe, string name, IEnumerable<string> lines)
    {
        var folder = Path.Combine(_factory.MarketDataRoot, "binance", "BTCUSDT", timeframe);
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, name), lines);
    }
}
