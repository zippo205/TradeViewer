using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Indicators.Handlers;
using TradeViewer.Application.Features.Indicators.Queries;
using TradeViewer.Contracts.Indicators;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.Indicators;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.MarketData.Store;
using TradeViewer.UnitTests.Application.Assets;
using TradeViewer.UnitTests.Domain.Indicators;

namespace TradeViewer.UnitTests.Application.Indicators;

/// <summary>/api/indicators at the level of its handler: the candle page, its warm-up, the current candle, errors.</summary>
public class GetIndicatorQueryHandlerTests
{
    private static readonly IReadOnlyList<Candle> Daily = IndicatorFixture.Daily;

    private readonly FakeAssetRepository _assets = new FakeAssetRepository();
    private readonly FakeCandleStore _store = new FakeCandleStore();

    public GetIndicatorQueryHandlerTests()
    {
        var info = new SymbolInfo("binance", "BTCUSDT", "BTC/USDT", AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
        var history = new AssetHistory(new Dictionary<Timeframe, DateOnly?> { [Timeframe.D1] = null, [Timeframe.W1] = null });
        _assets.Assets.Add(new Asset(info, history, 1, true));
        _store.Candles[new SeriesKey("binance", "BTCUSDT", Timeframe.D1)] = Daily.ToList();
    }

    [Theory]
    [InlineData("sma")]
    [InlineData("ema")]
    [InlineData("bb")]
    [InlineData("rsi")]
    [InlineData("macd")]
    [InlineData("atr")]
    public async Task The_latest_page_has_one_value_per_candle_equal_to_the_whole_history(string type)
    {
        var response = (await GetAsync(Query(type) with { Limit = 300 })).Value;

        var whole = Whole(type);
        Assert.Equal(Daily.TakeLast(300).Select(c => c.Time), response.T);
        Assert.False(response.Partial);
        Assert.Equal(whole.Select(l => l.Name), response.Lines.Keys);
        foreach (var line in whole)
        {
            AssertClose(line.Values.TakeLast(300), response.Lines[line.Name]);
        }
    }

    [Fact]
    public async Task Two_pages_meet_without_a_step()
    {
        var latest = (await GetAsync(Query("macd") with { Limit = 400 })).Value;
        var older = (await GetAsync(Query("macd") with { Limit = 400, To = latest.T[0] })).Value;

        var whole = Whole("macd");
        Assert.Equal(latest.T[0] - 86400, older.T[^1]);
        foreach (var line in whole)
        {
            AssertClose(line.Values.TakeLast(800), older.Lines[line.Name].Concat(latest.Lines[line.Name]));
        }
    }

    [Fact]
    public async Task At_the_start_of_the_history_the_values_are_null_until_the_indicator_is_defined()
    {
        var first = (await GetAsync(Query("sma") with { Limit = 20, To = Daily[20].Time })).Value;

        Assert.Equal(Daily.Take(20).Select(c => c.Time), first.T);
        Assert.All(first.Lines["value"].Take(8), v => Assert.Null(v));
        Assert.All(first.Lines["value"].Skip(8), v => Assert.NotNull(v));
    }

    [Fact]
    public async Task Parameters_left_out_take_the_defaults_and_given_ones_count()
    {
        var defaults = (await GetAsync(Query("bb") with { Limit = 50 })).Value;
        var given = (await GetAsync(Query("bb") with { Limit = 50, Length = 20, Deviation = 2, Price = "close" })).Value;
        var other = (await GetAsync(Query("bb") with { Limit = 50, Length = 50, Deviation = 3, Price = "hl2" })).Value;

        Assert.Equal(defaults.Lines["upper"], given.Lines["upper"]);
        AssertClose(Indicator.Calculate(Daily, new BollingerBandsParameters(50, 3, PriceSource.Hl2))[1].Values.TakeLast(50), other.Lines["upper"]);
    }

    [Fact]
    public async Task The_current_candle_has_its_value_too()
    {
        // The weekly series is the larger timeframe: its latest page ends with the current week built from the days.
        var weeks = Daily.Where(c => DateTimeOffset.FromUnixTimeSeconds(c.Time).DayOfWeek == DayOfWeek.Monday)
            .Select(monday => Week(monday.Time))
            .Where(week => week.Time + 7 * 86400 <= Daily[^1].Time + 86400)
            .ToList();
        _store.Candles[new SeriesKey("binance", "BTCUSDT", Timeframe.W1)] = weeks;

        var response = (await GetAsync(Query("sma") with { Timeframe = "1w", Limit = 30 })).Value;

        Assert.True(response.Partial);
        Assert.Equal(weeks[^1].Time + 7 * 86400, response.T[^1]);
        var current = Daily.Where(c => c.Time >= response.T[^1]).ToList();
        var closes = weeks.TakeLast(8).Select(w => w.Close).Append(current[^1].Close);
        Assert.Equal(closes.Average(), response.Lines["value"][^1]!.Value, 6);
    }

    [Theory]
    [InlineData(null, null, null, null, "type")]
    [InlineData("wma", null, null, null, "type")]
    [InlineData("sma", 0, null, null, "length")]
    [InlineData("sma", 5001, null, null, "length")]
    [InlineData("sma", null, "Close", null, "price")]
    [InlineData("sma", null, null, 12, "fast")]
    [InlineData("atr", null, "close", null, "price")]
    [InlineData("macd", 14, null, null, "length")]
    [InlineData("macd", null, null, 30, "fast")]
    public async Task Wrong_parameters_are_refused_by_field(string? type, int? length, string? price, int? fast, string field)
    {
        var result = await GetAsync(Query(type) with { Length = length, Price = price, Fast = fast });

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(field, result.Error.Fields.Keys);
    }

    [Fact]
    public async Task The_deviation_belongs_to_bollinger_bands_and_lies_within_its_range()
    {
        Assert.Contains("deviation", (await GetAsync(Query("bb") with { Deviation = 0 })).Error.Fields.Keys);
        Assert.Contains("deviation", (await GetAsync(Query("ema") with { Deviation = 2 })).Error.Fields.Keys);
    }

    [Fact]
    public async Task The_asset_timeframe_and_limit_are_checked_as_for_candles()
    {
        var unknownAsset = await GetAsync(Query("sma") with { Symbol = "ETHUSDT" });
        var notOfTheAsset = await GetAsync(Query("sma") with { Timeframe = "4h" });
        var tooMany = await GetAsync(Query("sma") with { Limit = 5001 });

        Assert.Equal(ErrorType.NotFound, unknownAsset.Error.Type);
        Assert.Equal(ErrorType.InvalidData, notOfTheAsset.Error.Type);
        Assert.Equal(ErrorType.Validation, tooMany.Error.Type);
    }

    private static GetIndicatorQuery Query(string? type) =>
        new("binance", "BTCUSDT", "1d", type, null, null, null, null, null, null, null, null);

    private static IReadOnlyList<IndicatorLine> Whole(string type)
    {
        Assert.True(IndicatorCodes.TryParse(type, out IndicatorType parsed));
        return Indicator.Calculate(Daily, IndicatorParameters.Default(parsed));
    }

    private Candle Week(long monday)
    {
        var days = Daily.Where(c => c.Time >= monday && c.Time < monday + 7 * 86400).ToList();
        return new Candle(monday, days[0].Open, days.Max(d => d.High), days.Min(d => d.Low), days[^1].Close, 0);
    }

    private static void AssertClose(IEnumerable<double?> expected, IEnumerable<double?> actual)
    {
        var e = expected.ToArray();
        var a = actual.ToArray();
        Assert.Equal(e.Length, a.Length);
        for (var i = 0; i < e.Length; i++)
        {
            Assert.Equal(e[i] == null, a[i] == null);
            if (e[i] is double x)
            {
                Assert.True(Math.Abs(a[i]!.Value - x) <= 1e-8 * Math.Max(1, Math.Abs(x)), $"value {i}: {a[i]} instead of {x}");
            }
        }
    }

    private Task<Result<IndicatorResponse>> GetAsync(GetIndicatorQuery query)
    {
        return new GetIndicatorQueryHandler(new CandleSeriesReader(_assets, _store)).HandleAsync(query, CancellationToken.None);
    }
}
