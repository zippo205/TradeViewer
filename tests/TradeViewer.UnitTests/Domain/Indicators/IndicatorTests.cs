using TradeViewer.Domain.Indicators;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.UnitTests.Domain.Indicators;

public class IndicatorTests
{
    /// <summary>BINANCE BTCUSDT 1d, 2017-08-17 to 2025-01-01 (tests/fixtures/indicators).</summary>
    private static readonly IReadOnlyList<Candle> Daily = IndicatorFixture.Daily;

    public static TheoryData<IndicatorType, string, string, double> TradingViewValues => new()
    {
        // The Data Window of TradingView, BINANCE:BTCUSDT 1D, defaults of IN1, read by the owner on 2026-11-18.
        { IndicatorType.Sma, "2020-03-12", IndicatorLines.Value, 8046.26 },
        { IndicatorType.Ema, "2020-03-12", IndicatorLines.Value, 7642.72 },
        { IndicatorType.BollingerBands, "2020-03-12", IndicatorLines.Basis, 8600.39 },
        { IndicatorType.BollingerBands, "2020-03-12", IndicatorLines.Upper, 10673.63 },
        { IndicatorType.BollingerBands, "2020-03-12", IndicatorLines.Lower, 6527.15 },
        { IndicatorType.Rsi, "2020-03-12", IndicatorLines.Value, 15.04 },
        { IndicatorType.Macd, "2020-03-12", IndicatorLines.Macd, -625.32 },
        { IndicatorType.Macd, "2020-03-12", IndicatorLines.Signal, -318.32 },
        { IndicatorType.Macd, "2020-03-12", IndicatorLines.Histogram, -307.00 },
        { IndicatorType.Atr, "2020-03-12", IndicatorLines.Value, 641.76 },
        { IndicatorType.Sma, "2025-01-01", IndicatorLines.Value, 95353.54 },
        { IndicatorType.Ema, "2025-01-01", IndicatorLines.Value, 95027.70 },
        { IndicatorType.BollingerBands, "2025-01-01", IndicatorLines.Basis, 98025.68 },
        { IndicatorType.BollingerBands, "2025-01-01", IndicatorLines.Upper, 106081.01 },
        { IndicatorType.BollingerBands, "2025-01-01", IndicatorLines.Lower, 89970.34 },
        { IndicatorType.Rsi, "2025-01-01", IndicatorLines.Value, 46.13 },
        { IndicatorType.Macd, "2025-01-01", IndicatorLines.Macd, -714.81 },
        { IndicatorType.Macd, "2025-01-01", IndicatorLines.Signal, 113.28 },
        { IndicatorType.Macd, "2025-01-01", IndicatorLines.Histogram, -828.09 },
        { IndicatorType.Atr, "2025-01-01", IndicatorLines.Value, 3795.47 }
    };

    [Theory]
    [MemberData(nameof(TradingViewValues))]
    public void The_values_equal_TradingView_to_the_cent(IndicatorType type, string day, string line, double expected)
    {
        var lines = Indicator.Calculate(Daily, IndicatorParameters.Default(type));

        var value = Value(lines, line, IndexOf(Daily, day));

        Assert.NotNull(value);
        Assert.Equal(expected, Math.Round(value.Value, 2, MidpointRounding.AwayFromZero), 2);
    }

    [Theory]
    // TradingView's history of the pair starts on 2017-10-01; there it showed the first values on these days.
    [InlineData(IndicatorType.Sma, IndicatorLines.Value, "2017-10-09")]
    [InlineData(IndicatorType.Ema, IndicatorLines.Value, "2017-10-09")]
    [InlineData(IndicatorType.Atr, IndicatorLines.Value, "2017-10-14")]
    [InlineData(IndicatorType.Rsi, IndicatorLines.Value, "2017-10-15")]
    [InlineData(IndicatorType.BollingerBands, IndicatorLines.Basis, "2017-10-20")]
    [InlineData(IndicatorType.BollingerBands, IndicatorLines.Upper, "2017-10-20")]
    [InlineData(IndicatorType.Macd, IndicatorLines.Macd, "2017-10-26")]
    [InlineData(IndicatorType.Macd, IndicatorLines.Signal, "2017-11-03")]
    [InlineData(IndicatorType.Macd, IndicatorLines.Histogram, "2017-11-03")]
    public void The_first_value_comes_on_the_candle_TradingView_shows_it(IndicatorType type, string line, string firstDay)
    {
        var fromTradingViewsStart = Daily.Skip(IndexOf(Daily, "2017-10-01")).ToList();

        var values = Line(Indicator.Calculate(fromTradingViewsStart, IndicatorParameters.Default(type)), line);

        var first = Array.FindIndex(values, v => v != null);
        Assert.Equal(firstDay, Day(fromTradingViewsStart[first].Time));
        Assert.All(values.Skip(first), v => Assert.NotNull(v));
    }

    public static TheoryData<IndicatorParameters> SeamParameters => new()
    {
        IndicatorParameters.Default(IndicatorType.Sma),
        IndicatorParameters.Default(IndicatorType.Ema),
        IndicatorParameters.Default(IndicatorType.BollingerBands),
        IndicatorParameters.Default(IndicatorType.Rsi),
        IndicatorParameters.Default(IndicatorType.Macd),
        IndicatorParameters.Default(IndicatorType.Atr),
        new SmaParameters(200, PriceSource.Hlc3),
        new EmaParameters(50, PriceSource.Ohlc4),
        new BollingerBandsParameters(50, 2.5, PriceSource.Hl2),
        new RsiParameters(2, PriceSource.Open),
        new MacdParameters(5, 35, 5, PriceSource.High),
        new AtrParameters(1)
    };

    [Theory]
    [MemberData(nameof(SeamParameters))]
    public void A_page_calculated_with_its_warm_up_equals_the_whole_history(IndicatorParameters parameters)
    {
        var whole = Indicator.Calculate(Daily, parameters);
        var warmUp = WarmUp.Candles(parameters);

        foreach (var pageStart in new[] { 1500, 2000, Daily.Count - 300 })
        {
            var from = pageStart - warmUp;
            Assert.True(from >= 0, "the fixture is long enough for the warm-up");
            var page = Indicator.Calculate(Daily.Skip(from).Take(warmUp + 300).ToList(), parameters);

            foreach (var line in whole)
            {
                for (var i = 0; i < 300 && pageStart + i < Daily.Count; i++)
                {
                    var expected = line.Values[pageStart + i]!.Value;
                    var actual = Line(page, line.Name)[warmUp + i]!.Value;
                    Assert.True(Math.Abs(actual - expected) <= 1e-8 * Math.Max(1, Math.Abs(expected)),
                        $"{line.Name} at {Day(Daily[pageStart + i].Time)}: {actual} instead of {expected}");
                }
            }
        }
    }

    [Fact]
    public void Sma_is_the_mean_of_the_last_n_prices()
    {
        var values = Line(Indicator.Calculate(Closes(1, 2, 3, 4, 5), new SmaParameters(3, PriceSource.Close)), IndicatorLines.Value);

        Assert.Equal(new double?[] { null, null, 2, 3, 4 }, values);
    }

    [Fact]
    public void Ema_starts_with_the_sma_and_then_smooths()
    {
        // alpha = 2 / (3 + 1) = 0.5; the seed is the mean of 1, 2, 3.
        var values = Line(Indicator.Calculate(Closes(1, 2, 3, 7, 5), new EmaParameters(3, PriceSource.Close)), IndicatorLines.Value);

        Assert.Equal(new double?[] { null, null, 2, 4.5, 4.75 }, values);
    }

    [Fact]
    public void Length_one_gives_the_price_itself()
    {
        var candles = Closes(3, 1, 4, 1, 5);

        Assert.Equal(new double?[] { 3, 1, 4, 1, 5 }, Line(Indicator.Calculate(candles, new SmaParameters(1, PriceSource.Close)), IndicatorLines.Value));
        Assert.Equal(new double?[] { 3, 1, 4, 1, 5 }, Line(Indicator.Calculate(candles, new EmaParameters(1, PriceSource.Close)), IndicatorLines.Value));
    }

    [Fact]
    public void Bollinger_bands_use_the_population_standard_deviation()
    {
        // 2, 4, 4, 4, 5, 5, 7, 9: mean 5, population standard deviation exactly 2.
        var lines = Indicator.Calculate(Closes(2, 4, 4, 4, 5, 5, 7, 9), new BollingerBandsParameters(8, 1.5, PriceSource.Close));

        Assert.Equal(5, Line(lines, IndicatorLines.Basis)[7]);
        Assert.Equal(8, Line(lines, IndicatorLines.Upper)[7]);
        Assert.Equal(2, Line(lines, IndicatorLines.Lower)[7]);
        Assert.Null(Line(lines, IndicatorLines.Upper)[6]);
    }

    [Fact]
    public void Rsi_uses_wilders_smoothing_of_gains_and_losses()
    {
        // Changes +2, -1, +3, -2: seed gain (2 + 0) / 2 = 1, loss (0 + 1) / 2 = 0.5 -> 100 - 100 / 3.
        // Then gain (3 + 1) / 2 = 2, loss (0 + 0.5) / 2 = 0.25 -> 100 - 100 / 9; then gain 1, loss 1.125.
        var values = Line(Indicator.Calculate(Closes(10, 12, 11, 14, 12), new RsiParameters(2, PriceSource.Close)), IndicatorLines.Value);

        Assert.Null(values[0]);
        Assert.Null(values[1]);
        Assert.Equal(100 - 100 / 3.0, values[2]!.Value, 10);
        Assert.Equal(100 - 100 / 9.0, values[3]!.Value, 10);
        Assert.Equal(100 - 100 / (1 + 1 / 1.125), values[4]!.Value, 10);
    }

    [Fact]
    public void Rsi_is_100_without_losses_and_0_without_gains()
    {
        Assert.Equal(100, Line(Indicator.Calculate(Closes(1, 2, 3, 4), new RsiParameters(2, PriceSource.Close)), IndicatorLines.Value)[3]);
        Assert.Equal(100, Line(Indicator.Calculate(Closes(5, 5, 5, 5), new RsiParameters(2, PriceSource.Close)), IndicatorLines.Value)[3]);
        Assert.Equal(0, Line(Indicator.Calculate(Closes(4, 3, 2, 1), new RsiParameters(2, PriceSource.Close)), IndicatorLines.Value)[3]);
    }

    [Fact]
    public void Atr_starts_with_high_minus_low_and_takes_gaps_into_the_true_range()
    {
        var candles = new[]
        {
            new Candle(0, 10, 12, 9, 11, 0),   // true range 12 - 9 = 3
            new Candle(1, 15, 16, 14, 15, 0),  // gap up: 16 - 11 = 5
            new Candle(2, 15, 15.5, 14.5, 15, 0) // 1
        };

        var values = Line(Indicator.Calculate(candles, new AtrParameters(2)), IndicatorLines.Value);

        Assert.Equal(new double?[] { null, 4, 2.5 }, values);
    }

    [Fact]
    public void Macd_signal_waits_for_enough_macd_values()
    {
        var candles = Closes(Enumerable.Range(1, 10).Select(i => (double)(i * i)).ToArray());

        var lines = Indicator.Calculate(candles, new MacdParameters(2, 4, 3, PriceSource.Close));

        var macd = Line(lines, IndicatorLines.Macd);
        var signal = Line(lines, IndicatorLines.Signal);
        Assert.Equal(3, Array.FindIndex(macd, v => v != null));   // slow - 1
        Assert.Equal(5, Array.FindIndex(signal, v => v != null)); // slow - 1 + signal - 1
        Assert.Equal((macd[3] + macd[4] + macd[5]) / 3, signal[5]);
        Assert.Equal(macd[7] - signal[7], Line(lines, IndicatorLines.Histogram)[7]);
    }

    [Theory]
    [InlineData(PriceSource.Open, 10)]
    [InlineData(PriceSource.High, 14)]
    [InlineData(PriceSource.Low, 8)]
    [InlineData(PriceSource.Close, 12)]
    [InlineData(PriceSource.Hl2, 11)]
    [InlineData(PriceSource.Hlc3, 34 / 3.0)]
    [InlineData(PriceSource.Ohlc4, 11)]
    public void The_price_source_picks_or_combines_the_prices(PriceSource source, double expected)
    {
        var prices = Indicator.Prices(new[] { new Candle(0, 10, 14, 8, 12, 0) }, source);

        Assert.Equal(expected, prices[0]);
    }

    [Fact]
    public void Each_indicator_returns_its_lines_with_one_value_per_candle()
    {
        foreach (var type in Enum.GetValues<IndicatorType>())
        {
            var lines = Indicator.Calculate(Daily, IndicatorParameters.Default(type));

            Assert.Equal(IndicatorLines.Of(type), lines.Select(l => l.Name));
            Assert.All(lines, l => Assert.Equal(Daily.Count, l.Values.Length));
        }
    }

    [Fact]
    public void Fewer_candles_than_the_length_give_only_nulls()
    {
        foreach (var type in Enum.GetValues<IndicatorType>())
        {
            var lines = Indicator.Calculate(Daily.Take(5).ToList(), IndicatorParameters.Default(type));

            Assert.All(lines, l => Assert.All(l.Values, v => Assert.Null(v)));
        }

        Assert.All(Indicator.Calculate(Array.Empty<Candle>(), IndicatorParameters.Default(IndicatorType.Macd)), l => Assert.Empty(l.Values));
    }

    private static double?[] Line(IReadOnlyList<IndicatorLine> lines, string name) => lines.Single(l => l.Name == name).Values;

    private static double? Value(IReadOnlyList<IndicatorLine> lines, string name, int index) => Line(lines, name)[index];

    private static IReadOnlyList<Candle> Closes(params double[] closes) =>
        closes.Select((c, i) => new Candle(i * 86400L, c, c, c, c, 0)).ToList();

    private static int IndexOf(IReadOnlyList<Candle> candles, string day)
    {
        var time = IndicatorFixture.Time(day);
        for (var i = 0; i < candles.Count; i++)
        {
            if (candles[i].Time == time)
            {
                return i;
            }
        }

        throw new ArgumentException($"No candle on {day}.");
    }

    private static string Day(long time) => IndicatorFixture.Day(time);
}
