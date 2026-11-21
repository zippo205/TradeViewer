using TradeViewer.Domain.MarketData;
using TradeViewer.Web.Charting;

namespace TradeViewer.UnitTests.Web;

/// <summary>CP1 (decision 0008): the timeframe a chart shows when another asset is chosen.</summary>
public class TimeframeChoiceTests
{
    [Fact]
    public void Web_knows_the_same_timeframes_in_the_same_order_as_the_Domain()
    {
        Assert.Equal(Timeframes.All.Select(Timeframes.Code), TimeframeChoice.Order);
    }

    [Theory]
    [InlineData("1h", "15m,1h,4h,1d,1w", "1h")]   // the asset has it: it stays
    [InlineData("4h", "15m,1h,1d,1w", "1d")]      // a stock has no 4h: the next larger one
    [InlineData("15m", "1h,1d", "1h")]            // the next larger, not the largest
    [InlineData("1w", "15m,1h,1d", "1d")]         // nothing larger: the largest smaller one
    [InlineData("1d", "1w,15m", "1w")]            // the order of the list does not matter
    public void The_timeframe_stays_otherwise_the_next_larger_otherwise_the_largest_smaller(string current, string available, string expected)
    {
        Assert.Equal(expected, TimeframeChoice.For(current, available.Split(',')));
    }

    [Fact]
    public void An_asset_without_a_chartable_timeframe_gets_none()
    {
        Assert.Null(TimeframeChoice.For("1h", Array.Empty<string>()));
        Assert.Null(TimeframeChoice.For("1h", new[] { "1M" })); // a timeframe TradeViewer does not know
    }

    [Fact]
    public void Web_knows_the_length_of_every_timeframe_as_the_Domain()
    {
        foreach (var timeframe in Timeframes.All)
        {
            Assert.Equal(Timeframes.Seconds(timeframe), TimeframeChoice.Seconds(Timeframes.Code(timeframe)));
        }
    }

    [Fact]
    public void Going_down_a_level_keeps_the_middle_and_zooms_in_by_the_ratio_of_the_timeframes()
    {
        var middle = new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var from = middle - 60 * 86400;
        var to = middle + 60 * 86400; // 120 daily candles

        var setup = TimeframeChoice.CentredWindow(from, to, "1d", "4h")!.Value;
        var trigger = TimeframeChoice.CentredWindow(setup.From, setup.To, "4h", "1h")!.Value;

        Assert.Equal((middle - 10 * 86400, middle + 10 * 86400), setup); // 120 four-hour candles
        Assert.Equal((middle - 10 * 86400 / 4, middle + 10 * 86400 / 4), trigger);
    }

    [Fact]
    public void Going_up_zooms_out_and_a_tiny_window_keeps_at_least_ten_candles()
    {
        var middle = 1_714_521_600L;

        Assert.Equal((middle - 24 * 3600 * 10, middle + 24 * 3600 * 10), TimeframeChoice.CentredWindow(middle - 36000, middle + 36000, "1h", "1d"));
        Assert.Equal((middle - 5 * 86400, middle + 5 * 86400), TimeframeChoice.CentredWindow(middle - 3600, middle + 3600, "1h", "1d"));
        Assert.Null(TimeframeChoice.CentredWindow(0, 100, "1h", "3m"));
    }
}
