using TradeViewer.Domain.MarketData;

namespace TradeViewer.UnitTests.Domain.MarketData;

public class TimeframesTests
{
    [Theory]
    [InlineData(Timeframe.M15, "15m", 900)]
    [InlineData(Timeframe.H1, "1h", 3600)]
    [InlineData(Timeframe.H4, "4h", 14400)]
    [InlineData(Timeframe.D1, "1d", 86400)]
    [InlineData(Timeframe.W1, "1w", 604800)]
    public void Every_timeframe_has_its_code_and_length(Timeframe timeframe, string code, long seconds)
    {
        Assert.Equal(code, Timeframes.Code(timeframe));
        Assert.Equal(seconds, Timeframes.Seconds(timeframe));
        Assert.True(Timeframes.TryParse(code, out var parsed));
        Assert.Equal(timeframe, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1H")]
    [InlineData("5m")]
    [InlineData("1M")]
    [InlineData("15m ")]
    public void An_unknown_code_is_no_timeframe(string? code)
    {
        Assert.False(Timeframes.TryParse(code, out _));
    }

    [Fact]
    public void The_timeframes_are_listed_smallest_first()
    {
        Assert.Equal(Timeframes.All.OrderBy(Timeframes.Seconds), Timeframes.All);
        Assert.Equal(Enum.GetValues<Timeframe>().Length, Timeframes.All.Count);
    }

    [Theory]
    [InlineData(Timeframe.M15, "2026-11-04T15:22:10Z", "2026-11-04T15:15:00Z")]
    [InlineData(Timeframe.M15, "2026-11-04T15:15:00Z", "2026-11-04T15:15:00Z")]
    [InlineData(Timeframe.H1, "2026-11-04T15:59:59Z", "2026-11-04T15:00:00Z")]
    [InlineData(Timeframe.H4, "2026-11-04T15:22:10Z", "2026-11-04T12:00:00Z")]
    [InlineData(Timeframe.H4, "2026-11-04T03:59:59Z", "2026-11-04T00:00:00Z")]
    [InlineData(Timeframe.D1, "2026-11-04T23:59:59Z", "2026-11-04T00:00:00Z")]
    [InlineData(Timeframe.W1, "2026-11-04T15:22:10Z", "2026-11-02T00:00:00Z")] // a Wednesday -> its Monday
    [InlineData(Timeframe.W1, "2026-11-02T00:00:00Z", "2026-11-02T00:00:00Z")] // a Monday
    [InlineData(Timeframe.W1, "2026-11-08T23:59:59Z", "2026-11-02T00:00:00Z")] // the Sunday of that week
    [InlineData(Timeframe.W1, "2017-08-17T04:00:00Z", "2017-08-14T00:00:00Z")]
    public void A_time_belongs_to_the_candle_that_opened_at_or_before_it(Timeframe timeframe, string time, string open)
    {
        var openTime = Timeframes.OpenTime(timeframe, Seconds(time));

        Assert.Equal(Seconds(open), openTime);
        Assert.Equal(Seconds(open) + Timeframes.Seconds(timeframe), Timeframes.CloseTime(timeframe, openTime));
    }

    [Fact]
    public void Weekly_candles_open_on_Mondays_also_before_1970()
    {
        var monday = Timeframes.OpenTime(Timeframe.W1, -1);

        Assert.Equal(DayOfWeek.Monday, DateTimeOffset.FromUnixTimeSeconds(monday).DayOfWeek);
        Assert.Equal(Seconds("1969-12-29T00:00:00Z"), monday);
    }

    private static long Seconds(string time)
    {
        return DateTimeOffset.Parse(time, System.Globalization.CultureInfo.InvariantCulture).ToUnixTimeSeconds();
    }
}
