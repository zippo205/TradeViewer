using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.MarketData.Sync;

namespace TradeViewer.UnitTests.Infrastructure.MarketData;

public class SyncScheduleTests
{
    [Theory]
    [InlineData(Timeframe.M15, "15:22:10", "15:15:30", "15:30:30")]
    [InlineData(Timeframe.M15, "15:15:30", "15:15:30", "15:30:30")]
    [InlineData(Timeframe.M15, "15:15:29", "15:00:30", "15:15:30")]
    [InlineData(Timeframe.M15, "00:00:10", "23:45:30", "00:00:30")] // the last due time was yesterday
    [InlineData(Timeframe.H1, "15:22:10", "15:00:30", "16:00:30")]
    [InlineData(Timeframe.H4, "15:22:10", "12:00:30", "16:00:30")]
    [InlineData(Timeframe.D1, "15:22:10", "00:00:30", "00:00:30")]
    public void New_candles_are_due_30_seconds_after_every_close_of_the_smallest_timeframe(Timeframe smallest, string now, string last, string next)
    {
        var time = At(now);

        var lastDue = SyncSchedule.LastDue(time, smallest);
        var nextDue = SyncSchedule.NextDue(time, smallest);

        Assert.Equal(TimeOnly.Parse(last), TimeOnly.FromDateTime(lastDue.UtcDateTime));
        Assert.Equal(TimeOnly.Parse(next), TimeOnly.FromDateTime(nextDue.UtcDateTime));
        Assert.True(lastDue <= time);
        Assert.True(nextDue > time);
        Assert.Equal(Timeframes.Seconds(smallest), (nextDue - lastDue).TotalSeconds);
    }

    private static DateTimeOffset At(string time)
    {
        return new DateTimeOffset(new DateOnly(2026, 11, 4).ToDateTime(TimeOnly.Parse(time), DateTimeKind.Utc));
    }
}
