using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.UnitTests.Domain.Assets;

public class HistoryTests
{
    private static readonly DateOnly Today = new DateOnly(2026, 11, 15);

    [Fact]
    public void The_initial_default_history_is_the_one_of_the_specification()
    {
        var history = HistorySettings.Initial.StartingAt(Today, Timeframes.All);

        Assert.Equal(new DateOnly(2026, 8, 17), history.StartOf(Timeframe.M15)); // 90 days
        Assert.Equal(new DateOnly(2025, 11, 15), history.StartOf(Timeframe.H1)); // one year
        Assert.Equal(new DateOnly(2024, 11, 15), history.StartOf(Timeframe.H4)); // two years
        Assert.Null(history.StartOf(Timeframe.D1));
        Assert.Null(history.StartOf(Timeframe.W1));
    }

    [Fact]
    public void An_asset_gets_only_the_timeframes_of_its_source()
    {
        var history = HistorySettings.Initial.StartingAt(Today, new[] { Timeframe.D1, Timeframe.H1 });

        Assert.Equal(new[] { Timeframe.H1, Timeframe.D1 }, history.Timeframes);
        Assert.True(history.Has(Timeframe.H1));
        Assert.False(history.Has(Timeframe.M15));
    }

    [Fact]
    public void A_timeframe_without_a_setting_gets_the_complete_history()
    {
        var settings = new HistorySettings(new Dictionary<Timeframe, HistoryDepth> { [Timeframe.M15] = HistoryDepth.OfDays(30) });

        Assert.True(settings.DepthOf(Timeframe.H4).IsComplete);
        Assert.Equal(30, settings.DepthOf(Timeframe.M15).Days);
    }

    [Fact]
    public void A_history_of_less_than_a_day_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HistoryDepth.OfDays(0));
    }

    [Fact]
    public void Files_are_wanted_from_the_start_day_on()
    {
        var history = HistorySettings.Initial.StartingAt(Today, Timeframes.All); // 15m from 2026-08-17

        Assert.False(history.Wants(File("BTCUSDT-15m-2026-08-16.csv")));
        Assert.True(history.Wants(File("BTCUSDT-15m-2026-08-17.csv")));
        Assert.True(history.Wants(File("BTCUSDT-15m-2026-11-15.csv")));
        Assert.True(history.Wants(File("BTCUSDT-1d-2017-08-17.csv"))); // complete history
    }

    [Fact]
    public void A_file_that_reaches_over_the_start_is_wanted()
    {
        var history = new AssetHistory(new Dictionary<Timeframe, DateOnly?> { [Timeframe.W1] = new DateOnly(2026, 10, 20) });

        Assert.True(history.Wants(File("BTCUSDT-1w-2026-10.csv")));
        Assert.False(history.Wants(File("BTCUSDT-1w-2026-09.csv")));
    }

    [Fact]
    public void A_file_of_a_timeframe_the_asset_does_not_have_is_not_wanted()
    {
        var history = new AssetHistory(new Dictionary<Timeframe, DateOnly?> { [Timeframe.D1] = null });

        Assert.False(history.Wants(File("BTCUSDT-15m-2026-11-15.csv")));
    }

    [Fact]
    public void Changing_one_timeframe_leaves_the_others()
    {
        var history = HistorySettings.Initial.StartingAt(Today, Timeframes.All);

        var changed = history.With(Timeframe.M15, new DateOnly(2023, 1, 1));

        Assert.Equal(new DateOnly(2023, 1, 1), changed.StartOf(Timeframe.M15));
        Assert.Equal(1672531200, changed.StartSecondsOf(Timeframe.M15));
        Assert.Equal(history.StartOf(Timeframe.H1), changed.StartOf(Timeframe.H1));
        Assert.Equal(new DateOnly(2026, 8, 17), history.StartOf(Timeframe.M15)); // the original is unchanged
        Assert.Null(changed.StartSecondsOf(Timeframe.D1));
    }

    [Theory]
    [InlineData(AssetClass.Crypto, "crypto")]
    [InlineData(AssetClass.Stock, "stock")]
    [InlineData(AssetClass.Etf, "etf")]
    [InlineData(AssetClass.Forex, "forex")]
    [InlineData(AssetClass.Commodity, "commodity")]
    public void Asset_classes_have_the_codes_of_the_Downloader(AssetClass assetClass, string code)
    {
        Assert.Equal(code, AssetCodes.Code(assetClass));
        Assert.True(AssetCodes.TryParse(code, out AssetClass parsed));
        Assert.Equal(assetClass, parsed);
        Assert.False(AssetCodes.TryParse(code.ToUpperInvariant(), out AssetClass _));
    }

    [Theory]
    [InlineData(SessionType.Continuous, "continuous")]
    [InlineData(SessionType.Exchange, "exchange")]
    [InlineData(SessionType.NewYorkClose, "newYorkClose")]
    public void Session_types_have_the_codes_of_the_Downloader(SessionType type, string code)
    {
        Assert.Equal(code, AssetCodes.Code(type));
        Assert.True(AssetCodes.TryParse(code, out SessionType parsed));
        Assert.Equal(type, parsed);
        Assert.False(AssetCodes.TryParse("other", out SessionType _));
    }

    private static CandleFile File(string name)
    {
        CandleFileRules.TryParseName(name, out var file);
        return file!;
    }
}
