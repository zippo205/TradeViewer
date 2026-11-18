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
}
