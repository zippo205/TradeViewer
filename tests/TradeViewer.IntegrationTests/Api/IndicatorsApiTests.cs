using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.Indicators;
using TradeViewer.Contracts.MarketData;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>/api/indicators through the real application: the same candles as /api/candles, one value per candle (IN1).</summary>
public class IndicatorsApiTests : IAsyncLifetime
{
    private readonly TradeViewerFactory _factory = new TradeViewerFactory();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        Copy("15m", "BTCUSDT-15m-2024-12-31.csv");
        Copy("15m", "BTCUSDT-15m-2025-01-01.csv");

        // The hours of 2024-12-31 only: the last hour of the 15m data is the current 1h candle (as in CandlesApiTests).
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
    public async Task An_indicator_comes_as_columns_with_the_times_of_the_candle_page()
    {
        var response = await _client.GetAsync("/api/indicators/binance/BTCUSDT/15m?type=bb&limit=100");
        var json = await response.Content.ReadAsStringAsync();
        var indicator = Read(json);
        var candles = await _client.GetFromJsonAsync<CandlesResponse>("/api/candles/binance/BTCUSDT/15m?limit=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("{\"t\":[", json);
        Assert.Contains("\"lines\":{\"basis\":[", json);
        Assert.Equal(candles!.T, indicator.T);
        Assert.Equal(new[] { "basis", "upper", "lower" }, indicator.Lines.Keys);
        Assert.All(indicator.Lines.Values, line => Assert.Equal(100, line.Length));

        // The warm-up comes from before the page: the first value is defined and is the mean of the 20 closes up to it.
        var before = await _client.GetFromJsonAsync<CandlesResponse>("/api/candles/binance/BTCUSDT/15m?limit=20&to=" + (candles.T[0] + 1));
        Assert.Equal(before!.C.Average(), indicator.Lines["basis"][0]!.Value, 6);
    }

    [Fact]
    public async Task At_the_start_of_the_history_the_values_are_null()
    {
        var json = await _client.GetStringAsync("/api/indicators/binance/BTCUSDT/15m?type=sma&length=9&limit=100&to=1735612200");
        var indicator = Read(json);

        Assert.Equal(1735603200, indicator.T[0]); // 2024-12-31 00:00, the first candle of the files
        Assert.StartsWith("{\"t\":[1735603200,", json);
        Assert.Contains("\"value\":[null,null,null,null,null,null,null,null,", json);
        Assert.NotNull(indicator.Lines["value"][8]);
    }

    [Fact]
    public async Task The_current_candle_is_included_and_marked()
    {
        var indicator = Read(await _client.GetStringAsync("/api/indicators/binance/BTCUSDT/1h?type=sma&length=2"));
        var candles = await _client.GetFromJsonAsync<CandlesResponse>("/api/candles/binance/BTCUSDT/1h");

        Assert.True(indicator.Partial);
        Assert.Equal(candles!.T, indicator.T);
        Assert.Equal((candles.C[^2] + candles.C[^1]) / 2, indicator.Lines["value"][^1]!.Value, 8);
    }

    [Fact]
    public async Task An_older_page_meets_the_latest_one_without_a_step()
    {
        var whole = Read(await _client.GetStringAsync("/api/indicators/binance/BTCUSDT/15m?type=sma&length=20&limit=192"));
        var latest = Read(await _client.GetStringAsync("/api/indicators/binance/BTCUSDT/15m?type=sma&length=20&limit=50"));
        var older = Read(await _client.GetStringAsync("/api/indicators/binance/BTCUSDT/15m?type=sma&length=20&limit=50&to=" + latest.T[0]));

        Assert.Equal(whole.Lines["value"].TakeLast(100), older.Lines["value"].Concat(latest.Lines["value"]));
    }

    [Fact]
    public async Task Wrong_requests_are_answered_with_problem_details()
    {
        var unknownAsset = await _client.GetAsync("/api/indicators/binance/ETHUSDT/15m?type=sma");
        var noTimeframe = await _client.GetAsync("/api/indicators/binance/BTCUSDT/7m?type=sma");
        var noType = await _client.GetAsync("/api/indicators/binance/BTCUSDT/15m");
        var wrongLengths = await _client.GetAsync("/api/indicators/binance/BTCUSDT/15m?type=macd&fast=30&slow=26");
        var notANumber = await _client.GetAsync("/api/indicators/binance/BTCUSDT/15m?type=sma&length=nine");

        Assert.Equal(HttpStatusCode.NotFound, unknownAsset.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noTimeframe.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noType.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongLengths.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notANumber.StatusCode);
        Assert.All(
            new[] { unknownAsset, noTimeframe, noType, wrongLengths, notANumber },
            response => Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType));

        var problem = JsonDocument.Parse(await wrongLengths.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("fast", out _));
        Assert.True(JsonDocument.Parse(await noType.Content.ReadAsStringAsync()).RootElement.GetProperty("errors").TryGetProperty("type", out _));
    }

    private static IndicatorResponse Read(string json) =>
        JsonSerializer.Deserialize<IndicatorResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

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
