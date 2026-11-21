using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.MarketData;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Web.Scenarios;

namespace TradeViewer.UnitTests.Web;

/// <summary>SC6: after the history is loaded the candles of the three levels must cover the span of a scenario.</summary>
public class ScenarioCoverageTests
{
    private static readonly DateTimeOffset From = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new DateTimeOffset(2024, 8, 31, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<ScenarioLevelRecord> Levels = new[]
    {
        new ScenarioLevelRecord("context", "1d"), new ScenarioLevelRecord("setup", "4h"), new ScenarioLevelRecord("trigger", "1h")
    };

    [Fact]
    public void Candles_from_before_the_span_to_now_cover_it()
    {
        var asset = Asset("crypto", ("1d", From.AddYears(-3)), ("4h", From), ("1h", From.AddMinutes(30)));

        Assert.Empty(ScenarioCoverage.Missing(asset, Levels, From, To, Now));
    }

    [Fact]
    public void A_level_whose_candles_start_after_the_span_is_named()
    {
        var asset = Asset("crypto", ("1d", From), ("4h", From), ("1h", From.AddDays(40)));

        var gap = Assert.Single(ScenarioCoverage.Missing(asset, Levels, From, To, Now));

        Assert.Equal(("trigger", "1h"), (gap.Level, gap.Timeframe));
        Assert.Contains("2024-02-10", gap.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_without_candles_is_named()
    {
        var asset = Asset("crypto", ("1d", From), ("4h", From));

        Assert.Equal("1h", Assert.Single(ScenarioCoverage.Missing(asset, Levels, From, To, Now)).Timeframe);
    }

    [Fact]
    public void Candles_that_end_before_the_span_ends_do_not_cover_it_but_a_span_into_the_future_ends_now()
    {
        var ended = Asset("crypto", ("1d", From), ("4h", From), ("1h", From));
        var shortHistory = ended with
        {
            Timeframes = ended.Timeframes.Select(timeframe => timeframe with { LastCandle = To.AddDays(-20) }).ToList()
        };

        Assert.Equal(3, ScenarioCoverage.Missing(shortHistory, Levels, From, To, Now).Count);
        Assert.Empty(ScenarioCoverage.Missing(Asset("crypto", ("1d", From), ("4h", From), ("1h", From)), Levels, From, Now.AddDays(30), Now));
    }

    [Fact]
    public void An_asset_with_exchange_hours_may_start_after_a_weekend()
    {
        var monday = From.AddDays(3);

        Assert.Empty(ScenarioCoverage.Missing(Asset("stock", ("1d", monday), ("4h", monday), ("1h", monday)), Levels, From, To, Now));
        Assert.Equal(3, ScenarioCoverage.Missing(Asset("crypto", ("1d", monday), ("4h", monday), ("1h", monday)), Levels, From, To, Now).Count);
    }

    private static AssetResponse Asset(string assetClass, params (string Timeframe, DateTimeOffset First)[] stored)
    {
        var timeframes = stored
            .Select(item => new AssetTimeframeResponse(item.Timeframe, null, item.First, Now.AddHours(-1), 10, 10, false, true, Array.Empty<GapResponse>()))
            .ToList();
        return new AssetResponse("binance", "BTCUSDT", "BTC/USDT", assetClass, 0.01, 0, false, false, timeframes, Array.Empty<string>());
    }
}
