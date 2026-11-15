using TradeViewer.Domain.MarketData;

namespace TradeViewer.UnitTests.Domain.MarketData;

/// <summary>MD8: the current candle of a timeframe, built from closed candles of the smallest one.</summary>
public class TimeframeAggregatorTests
{
    private static readonly long Hour14 = Seconds("2026-11-04T14:00:00Z");
    private static readonly long Hour15 = Seconds("2026-11-04T15:00:00Z");

    [Fact]
    public void The_current_candle_takes_open_of_the_first_high_and_low_over_all_close_of_the_last_and_the_sum_of_the_volume()
    {
        var small = new[]
        {
            new Candle(Hour15, 100, 104, 99, 103, 10),
            new Candle(Hour15 + 900, 103, 110, 102, 108, 20),
            new Candle(Hour15 + 1800, 108, 109, 95, 96, 5.5)
        };

        var current = TimeframeAggregator.CurrentCandle(Timeframe.H1, Hour14, small);

        Assert.Equal(new Candle(Hour15, 100, 110, 95, 96, 35.5), current);
    }

    [Fact]
    public void Small_candles_before_the_current_period_are_not_part_of_it()
    {
        var small = new[]
        {
            new Candle(Hour14 + 2700, 1, 500, 0.5, 2, 1000), // 14:45, belongs to the closed hour
            new Candle(Hour15, 100, 104, 99, 103, 10)
        };

        var current = TimeframeAggregator.CurrentCandle(Timeframe.H1, Hour14, small);

        Assert.Equal(new Candle(Hour15, 100, 104, 99, 103, 10), current);
    }

    [Fact]
    public void There_is_no_current_candle_once_the_Downloader_delivered_the_candle_of_the_period()
    {
        var small = new[] { new Candle(Hour15, 100, 104, 99, 103, 10), new Candle(Hour15 + 2700, 103, 105, 101, 104, 10) };

        Assert.Null(TimeframeAggregator.CurrentCandle(Timeframe.H1, Hour15, small));
        Assert.Null(TimeframeAggregator.CurrentCandle(Timeframe.H1, Hour15 + 3600, small));
    }

    [Fact]
    public void Without_small_candles_there_is_no_current_candle()
    {
        Assert.Null(TimeframeAggregator.CurrentCandle(Timeframe.H1, Hour14, Array.Empty<Candle>()));
    }

    [Fact]
    public void A_period_the_Downloader_has_not_delivered_is_not_invented_only_the_newest_one_is_built()
    {
        // The 1h candle of 14:00 is late: 15m candles of 14:xx and 15:xx exist, the last closed hour is 13:00.
        var small = new[]
        {
            new Candle(Hour14, 50, 60, 40, 55, 1),
            new Candle(Hour14 + 2700, 55, 58, 50, 57, 1),
            new Candle(Hour15, 100, 104, 99, 103, 10)
        };

        var current = TimeframeAggregator.CurrentCandle(Timeframe.H1, Hour14 - 3600, small);

        Assert.Equal(new Candle(Hour15, 100, 104, 99, 103, 10), current);
    }

    [Fact]
    public void A_missing_small_candle_leaves_the_current_candle_incomplete_but_present()
    {
        var small = new[] { new Candle(Hour15 + 900, 103, 110, 102, 108, 20) }; // 15:00 is missing

        var current = TimeframeAggregator.CurrentCandle(Timeframe.H1, Hour14, small);

        Assert.Equal(new Candle(Hour15, 103, 110, 102, 108, 20), current);
    }

    [Fact]
    public void Without_any_closed_candle_the_current_one_is_still_built()
    {
        var small = new[] { new Candle(Hour15, 100, 104, 99, 103, 10) };

        Assert.Equal(new Candle(Hour15, 100, 104, 99, 103, 10), TimeframeAggregator.CurrentCandle(Timeframe.H1, null, small));
    }

    [Theory]
    [InlineData(Timeframe.H4, "2026-11-04T12:00:00Z")]
    [InlineData(Timeframe.D1, "2026-11-04T00:00:00Z")]
    [InlineData(Timeframe.W1, "2026-11-02T00:00:00Z")]
    public void The_current_candle_opens_where_the_timeframe_cuts_its_periods(Timeframe timeframe, string open)
    {
        var small = new[] { new Candle(Hour15, 100, 104, 99, 103, 10), new Candle(Hour15 + 900, 103, 105, 101, 104, 10) };

        var current = TimeframeAggregator.CurrentCandle(timeframe, null, small);

        Assert.Equal(Seconds(open), current!.Value.Time);
        Assert.Equal(20, current.Value.Volume);
    }

    private static long Seconds(string time)
    {
        return DateTimeOffset.Parse(time, System.Globalization.CultureInfo.InvariantCulture).ToUnixTimeSeconds();
    }
}
