using TradeViewer.Domain.Indicators;

namespace TradeViewer.UnitTests.Domain.Indicators;

public class IndicatorRulesTests
{
    [Fact]
    public void Every_new_indicator_is_valid()
    {
        foreach (var type in Enum.GetValues<IndicatorType>())
        {
            var instance = IndicatorInstance.New(Guid.NewGuid(), type);

            Assert.Empty(IndicatorRules.Check(instance));
            Assert.Equal(type, instance.Type);
            Assert.False(instance.Hidden);
        }
    }

    [Fact]
    public void The_defaults_are_those_of_TradingView()
    {
        Assert.Equal(new SmaParameters(9, PriceSource.Close), IndicatorParameters.Default(IndicatorType.Sma));
        Assert.Equal(new EmaParameters(9, PriceSource.Close), IndicatorParameters.Default(IndicatorType.Ema));
        Assert.Equal(new BollingerBandsParameters(20, 2, PriceSource.Close), IndicatorParameters.Default(IndicatorType.BollingerBands));
        Assert.Equal(new RsiParameters(14, PriceSource.Close), IndicatorParameters.Default(IndicatorType.Rsi));
        Assert.Equal(new MacdParameters(12, 26, 9, PriceSource.Close), IndicatorParameters.Default(IndicatorType.Macd));
        Assert.Equal(new AtrParameters(14), IndicatorParameters.Default(IndicatorType.Atr));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(IndicatorRules.MaxLength + 1)]
    public void A_length_outside_1_to_5000_is_refused(int length)
    {
        Assert.Contains("length", IndicatorRules.Check(new SmaParameters(length, PriceSource.Close)).Keys);
        Assert.Contains("length", IndicatorRules.Check(new AtrParameters(length)).Keys);
        Assert.Contains("signal", IndicatorRules.Check(new MacdParameters(12, 26, length, PriceSource.Close)).Keys);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(IndicatorRules.MaxLength)]
    public void Lengths_1_and_5000_are_allowed(int length)
    {
        Assert.Empty(IndicatorRules.Check(new EmaParameters(length, PriceSource.Close)));
        Assert.Empty(IndicatorRules.Check(new RsiParameters(length, PriceSource.Close)));
    }

    [Theory]
    [InlineData(26, 26)]
    [InlineData(30, 26)]
    public void Macd_needs_a_fast_length_shorter_than_the_slow_one(int fast, int slow)
    {
        var problems = IndicatorRules.Check(new MacdParameters(fast, slow, 9, PriceSource.Close));

        Assert.Equal(new[] { "fast" }, problems.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(50.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void The_deviation_of_bollinger_bands_is_finite_and_within_its_range(double deviation)
    {
        Assert.Equal(new[] { "deviation" }, IndicatorRules.Check(new BollingerBandsParameters(20, deviation, PriceSource.Close)).Keys);
    }

    [Fact]
    public void An_unknown_price_source_is_refused()
    {
        Assert.Equal(new[] { "source" }, IndicatorRules.Check(new SmaParameters(9, (PriceSource)99)).Keys);
    }

    [Fact]
    public void An_instance_reports_the_fields_of_its_parameters()
    {
        var instance = IndicatorInstance.New(Guid.Empty, IndicatorType.Rsi) with { Parameters = new RsiParameters(0, PriceSource.Close) };

        var problems = IndicatorRules.Check(instance);

        Assert.Equal(new[] { "id", "parameters.length" }, problems.Keys.Order());
    }

    [Fact]
    public void An_instance_has_exactly_the_style_entries_of_its_type()
    {
        var macd = IndicatorInstance.New(Guid.NewGuid(), IndicatorType.Macd);
        var withoutBelow = macd with { Styles = macd.Styles.Where(s => s.Line != IndicatorStyles.HistogramBelow).ToList() };
        var twice = macd with { Styles = macd.Styles.Append(macd.Styles[0]).ToList() };
        var foreign = macd with { Styles = IndicatorStyles.Default(IndicatorType.BollingerBands) };

        Assert.Contains("styles", IndicatorRules.Check(withoutBelow).Keys);
        Assert.Contains("styles", IndicatorRules.Check(twice).Keys);
        Assert.Contains("styles", IndicatorRules.Check(foreign).Keys);
    }

    [Fact]
    public void A_style_needs_a_colour_and_a_width_of_1_to_4()
    {
        var sma = IndicatorInstance.New(Guid.NewGuid(), IndicatorType.Sma);

        var problems = IndicatorRules.Check(sma with { Styles = new[] { new IndicatorLineStyle(IndicatorLines.Value, "blue", 5) } });

        Assert.Equal(new[] { "styles.value.color", "styles.value.width" }, problems.Keys.Order());
    }

    [Fact]
    public void A_template_needs_a_name_and_indicators_with_distinct_ids()
    {
        var id = Guid.NewGuid();
        var sma = IndicatorInstance.New(id, IndicatorType.Sma);
        var ema = IndicatorInstance.New(id, IndicatorType.Ema);

        Assert.Empty(IndicatorRules.Check(new IndicatorTemplate(Guid.NewGuid(), "Trend", new[] { sma })));
        Assert.Contains("name", IndicatorRules.Check(new IndicatorTemplate(Guid.NewGuid(), " ", new[] { sma })).Keys);
        Assert.Contains("name", IndicatorRules.Check(new IndicatorTemplate(Guid.NewGuid(), new string('x', 101), new[] { sma })).Keys);
        Assert.Contains("indicators", IndicatorRules.Check(new IndicatorTemplate(Guid.NewGuid(), "Empty", Array.Empty<IndicatorInstance>())).Keys);
        Assert.Contains("indicators[1].id", IndicatorRules.Check(new IndicatorTemplate(Guid.NewGuid(), "Twice", new[] { sma, ema })).Keys);
    }

    [Fact]
    public void Several_indicators_of_one_kind_are_allowed()
    {
        var sma50 = IndicatorInstance.New(Guid.NewGuid(), IndicatorType.Sma) with { Parameters = new SmaParameters(50, PriceSource.Close) };
        var sma200 = IndicatorInstance.New(Guid.NewGuid(), IndicatorType.Sma) with { Parameters = new SmaParameters(200, PriceSource.Close) };

        Assert.Empty(IndicatorRules.Check(new IndicatorTemplate(Guid.NewGuid(), "Golden cross", new[] { sma50, sma200 })));
    }

    [Fact]
    public void Codes_go_both_ways()
    {
        foreach (var type in Enum.GetValues<IndicatorType>())
        {
            Assert.True(IndicatorCodes.TryParse(IndicatorCodes.Code(type), out IndicatorType parsed));
            Assert.Equal(type, parsed);
        }

        foreach (var source in Enum.GetValues<PriceSource>())
        {
            Assert.True(IndicatorCodes.TryParse(IndicatorCodes.Code(source), out PriceSource parsed));
            Assert.Equal(source, parsed);
        }

        Assert.Equal("bb", IndicatorCodes.Code(IndicatorType.BollingerBands));
        Assert.Equal("hlc3", IndicatorCodes.Code(PriceSource.Hlc3));
        Assert.False(IndicatorCodes.TryParse("Close", out PriceSource _));
    }

    [Fact]
    public void The_warm_up_covers_the_look_back_and_the_memory_of_the_smoothing()
    {
        Assert.Equal(8, WarmUp.Candles(new SmaParameters(9, PriceSource.Close)));
        Assert.Equal(19, WarmUp.Candles(new BollingerBandsParameters(20, 2, PriceSource.Close)));
        Assert.InRange(WarmUp.Candles(new EmaParameters(9, PriceSource.Close)), 100, 120);
        Assert.InRange(WarmUp.Candles(new RsiParameters(14, PriceSource.Close)), 300, 340);
        Assert.InRange(WarmUp.Candles(new MacdParameters(12, 26, 9, PriceSource.Close)), 400, 450);
        Assert.Equal(0, WarmUp.Candles(new EmaParameters(1, PriceSource.Close)));
    }
}
