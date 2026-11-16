using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.UnitTests.Domain.Assets;

/// <summary>AS1 and AS6: the figures of a watchlist row and the rule for a new order.</summary>
public class WatchlistRulesTests
{
    private static readonly long Nov14 = Seconds("2026-11-14T00:00:00Z");
    private static readonly long Nov15 = Seconds("2026-11-15T00:00:00Z");

    [Fact]
    public void The_change_compares_the_last_close_with_the_close_of_the_day_before()
    {
        var last = Candle(Seconds("2026-11-15T19:15:00Z"), 66000);
        var daily = new[] { Candle(Nov14 - 86400, 70000), Candle(Nov14, 64000) }; // the 15th is still in progress, so not delivered

        var figures = RowFigures.Of(Timeframe.M15, last, daily);

        Assert.Equal(66000, figures!.LastClose);
        Assert.Equal(Seconds("2026-11-15T19:30:00Z"), figures.LastCloseTime); // a candle closes when the next one opens
        Assert.Equal(3.125, figures.ChangePercent!.Value, 10);
    }

    [Fact]
    public void A_daily_candle_of_the_same_day_is_not_the_previous_close()
    {
        // The Downloader has delivered the candle of the 15th already; the last 15m candle is its last quarter.
        var last = Candle(Seconds("2026-11-15T23:45:00Z"), 66000);
        var daily = new[] { Candle(Nov15, 66000), Candle(Nov14, 60000) };

        var figures = RowFigures.Of(Timeframe.M15, last, daily);

        Assert.Equal(10, figures!.ChangePercent!.Value, 10);
        Assert.Equal(Seconds("2026-11-16T00:00:00Z"), figures.LastCloseTime);
    }

    [Fact]
    public void An_asset_whose_smallest_timeframe_is_the_day_compares_two_daily_closes()
    {
        var daily = new[] { Candle(Nov14, 60000), Candle(Nov15, 57000) };

        var figures = RowFigures.Of(Timeframe.D1, daily[1], daily);

        Assert.Equal(-5, figures!.ChangePercent!.Value, 10);
        Assert.Equal(Nov15 + 86400, figures.LastCloseTime);
    }

    [Fact]
    public void Without_an_earlier_daily_candle_there_is_a_last_close_but_no_change()
    {
        var last = Candle(Seconds("2026-11-15T19:15:00Z"), 66000);

        Assert.Null(RowFigures.Of(Timeframe.M15, last, Array.Empty<Candle>())!.ChangePercent);
        Assert.Null(RowFigures.Of(Timeframe.M15, last, new[] { Candle(Nov15, 65000) })!.ChangePercent);
        Assert.Equal(66000, RowFigures.Of(Timeframe.M15, last, Array.Empty<Candle>())!.LastClose);
    }

    [Fact]
    public void Without_candles_there_are_no_figures()
    {
        Assert.Null(RowFigures.Of(Timeframe.M15, null, new[] { Candle(Nov14, 60000) }));
    }

    [Fact]
    public void A_new_order_must_name_exactly_the_assets_of_the_list()
    {
        var current = new[] { ("binance", "BTCUSDT"), ("binance", "ETHUSDT"), ("yahoo", "BTCUSDT") };

        Assert.True(WatchlistOrder.IsValid(current, new[] { ("yahoo", "BTCUSDT"), ("binance", "ETHUSDT"), ("binance", "BTCUSDT") }));
        Assert.True(WatchlistOrder.IsValid(Array.Empty<(string, string)>(), Array.Empty<(string, string)>()));
        Assert.False(WatchlistOrder.IsValid(current, new[] { ("binance", "ETHUSDT"), ("binance", "BTCUSDT") }));                                   // one missing
        Assert.False(WatchlistOrder.IsValid(current, new[] { ("binance", "BTCUSDT"), ("binance", "ETHUSDT"), ("binance", "SOLUSDT") }));           // a foreign one
        Assert.False(WatchlistOrder.IsValid(current, new[] { ("binance", "BTCUSDT"), ("binance", "BTCUSDT"), ("binance", "ETHUSDT") }));           // one twice
        Assert.False(WatchlistOrder.IsValid(current, new[] { ("binance", "BTCUSDT"), ("binance", "ETHUSDT"), ("yahoo", "BTCUSDT"), ("x", "y") })); // one more
    }

    [Fact]
    public void There_are_five_lists_one_per_asset_class()
    {
        Assert.Equal(new[] { "Crypto", "Stocks", "ETFs", "Forex", "Commodities" }, WatchlistOrder.Lists.Select(WatchlistOrder.Title));
        Assert.Equal(Enum.GetValues<AssetClass>().Length, WatchlistOrder.Lists.Count);
    }

    private static Candle Candle(long time, double close)
    {
        return new Candle(time, close, close + 1, close - 1, close, 1);
    }

    private static long Seconds(string time)
    {
        return DateTimeOffset.Parse(time, System.Globalization.CultureInfo.InvariantCulture).ToUnixTimeSeconds();
    }
}
