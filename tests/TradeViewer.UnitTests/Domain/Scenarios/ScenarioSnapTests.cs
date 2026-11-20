using TradeViewer.Domain.MarketData;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.UnitTests.Domain.Scenarios;

/// <summary>SC10: the snap rules of EWTS's format on the candles of the point's level.</summary>
public class ScenarioSnapTests
{
    private const long Day = 1_730_419_200; // 2024-11-01 00:00 UTC
    private const long Hour = 3600;

    private static readonly Candle Candle = new Candle(Day + (4 * Hour), Open: 100, High: 120, Low: 90, Close: 110, Volume: 5);

    [Theory]
    [InlineData(ScenarioPointName.Wave1Start, 90)]
    [InlineData(ScenarioPointName.Wave1End, 120)]
    [InlineData(ScenarioPointName.Wave2End, 90)]
    [InlineData(ScenarioPointName.Wave1I, 120)]
    [InlineData(ScenarioPointName.Wave1Ii, 90)]
    [InlineData(ScenarioPointName.Wave1V, 120)]
    [InlineData(ScenarioPointName.Wave1A, 120)]
    [InlineData(ScenarioPointName.Wave1B, 90)]
    [InlineData(ScenarioPointName.Wave2A, 90)]
    [InlineData(ScenarioPointName.Wave2B, 120)]
    [InlineData(ScenarioPointName.Wave2C, 90)]
    [InlineData(ScenarioPointName.Entry, 110)]
    [InlineData(ScenarioPointName.Add, 110)]
    [InlineData(ScenarioPointName.Exit, 117.5)]
    [InlineData(ScenarioPointName.StopMove, 117.5)]
    [InlineData(ScenarioPointName.Stop, 117.5)]
    public void A_point_takes_the_open_time_of_its_candle_and_the_price_of_its_rule(ScenarioPointName name, double price)
    {
        Assert.Equal(new ScenarioPoint(Day + (4 * Hour), price), ScenarioSnap.Snap(name, Candle, givenPrice: 117.5));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3 * Hour + 1799, 0)]
    [InlineData(4 * Hour, 4 * Hour)]
    [InlineData(12 * Hour - 1, 8 * Hour)]
    public void The_containing_candle_is_the_one_whose_period_holds_the_time(long offset, long open)
    {
        var candles = new[] { At(0), At(4 * Hour), At(8 * Hour) };

        Assert.Equal(Day + open, ScenarioSnap.ContainingCandle(candles, Timeframe.H4, Day + offset)!.Value.Time);
    }

    [Fact]
    public void Outside_the_candles_and_in_a_gap_there_is_no_candle()
    {
        var candles = new[] { At(0), At(8 * Hour) }; // the candle of 04:00 is missing

        Assert.Null(ScenarioSnap.ContainingCandle(candles, Timeframe.H4, Day + (5 * Hour)));
        Assert.Null(ScenarioSnap.ContainingCandle(candles, Timeframe.H4, Day - 1));
        Assert.Null(ScenarioSnap.ContainingCandle(candles, Timeframe.H4, Day + (12 * Hour)));
        Assert.Null(ScenarioSnap.ContainingCandle(Array.Empty<Candle>(), Timeframe.H4, Day));
    }

    private static Candle At(long offset)
    {
        return new Candle(Day + offset, 1, 2, 0.5, 1.5, 1);
    }
}
