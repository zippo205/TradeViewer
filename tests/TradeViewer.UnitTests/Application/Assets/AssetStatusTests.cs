using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Assets.Services;
using TradeViewer.Application.Features.Workspace.Services;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.UnitTests.Application.Assets;

/// <summary>AS4: when an asset and its timeframes count as loading, and when a timeframe can be charted.</summary>
public class AssetStatusTests
{
    private static readonly Asset Btc = new Asset(
        new SymbolInfo("binance", "BTCUSDT", "BTC/USDT", AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All),
        new AssetHistory(new Dictionary<Timeframe, DateOnly?> { [Timeframe.M15] = new DateOnly(2026, 8, 17), [Timeframe.D1] = null }),
        3,
        true);

    private static readonly IReadOnlyList<SeriesRange> NoRanges = Array.Empty<SeriesRange>();
    private static readonly IReadOnlyList<ImportLogEntry> NoErrors = Array.Empty<ImportLogEntry>();

    [Fact]
    public void An_asset_that_is_imported_and_not_downloaded_any_more_is_not_loading()
    {
        var ranges = new[] { new SeriesRange(Timeframe.M15, 1000, 2000), new SeriesRange(Timeframe.D1, 500, 2000) };
        var import = new ImportProgress(false, new[] { new TimeframeProgress(Timeframe.D1, 10, 10), new TimeframeProgress(Timeframe.M15, 90, 90) });

        var asset = AssetStatusReader.Build(Btc, ranges, import, Downloader(0, 0), NoErrors);

        Assert.False(asset.Loading);
        Assert.All(asset.Timeframes, timeframe => Assert.False(timeframe.Loading));
        Assert.All(asset.Timeframes, timeframe => Assert.True(timeframe.Chartable));
        Assert.Equal((90, 90), (asset.Timeframes[0].FilesImported, asset.Timeframes[0].FilesWanted));
        Assert.Equal(3, asset.Position);
    }

    [Fact]
    public void While_the_import_runs_a_finished_timeframe_can_be_charted_and_the_others_are_loading()
    {
        var ranges = new[] { new SeriesRange(Timeframe.D1, 500, 2000), new SeriesRange(Timeframe.M15, 1900, 2000) };
        var import = new ImportProgress(true, new[] { new TimeframeProgress(Timeframe.D1, 10, 10), new TimeframeProgress(Timeframe.M15, 20, 90) });

        var asset = AssetStatusReader.Build(Btc, ranges, import, null, NoErrors);

        var m15 = asset.Timeframes[0];
        var d1 = asset.Timeframes[1];
        Assert.True(asset.Loading);
        Assert.True(m15.Loading);
        Assert.True(m15.Chartable); // its newest candles are there already
        Assert.Equal((20, 90), (m15.FilesImported, m15.FilesWanted));
        Assert.False(d1.Loading);
        Assert.True(d1.Chartable);
    }

    [Fact]
    public void A_queued_asset_is_loading_in_every_timeframe_and_cannot_be_charted_yet()
    {
        var asset = AssetStatusReader.Build(Btc, NoRanges, new ImportProgress(true, Array.Empty<TimeframeProgress>()), null, NoErrors);

        Assert.True(asset.Loading);
        Assert.All(asset.Timeframes, timeframe => Assert.True(timeframe.Loading));
        Assert.All(asset.Timeframes, timeframe => Assert.False(timeframe.Chartable));
        Assert.All(asset.Timeframes, timeframe => Assert.Null(timeframe.LastCandle));
    }

    [Fact]
    public void A_timeframe_the_Downloader_still_downloads_is_loading_although_the_import_is_done()
    {
        var import = new ImportProgress(false, new[] { new TimeframeProgress(Timeframe.D1, 10, 10), new TimeframeProgress(Timeframe.M15, 5, 5) });

        var jobs = AssetStatusReader.Build(Btc, NoRanges, import, Downloader(pendingJobs15m: 4, pendingJobs1d: 0), NoErrors);
        var pendingGap = AssetStatusReader.Build(Btc, NoRanges, import, Downloader(0, 0, gapReason: "pending"), NoErrors);

        Assert.True(jobs.Loading);
        Assert.True(jobs.Timeframes[0].Loading);
        Assert.False(jobs.Timeframes[1].Loading);
        Assert.True(pendingGap.Timeframes[0].Loading);
    }

    [Theory]
    [InlineData("missingUpstream")]
    [InlineData("unavailable")]
    public void What_the_source_does_not_have_is_not_loading(string reason)
    {
        var import = new ImportProgress(false, Array.Empty<TimeframeProgress>());

        var asset = AssetStatusReader.Build(Btc, NoRanges, import, Downloader(0, 0, gapReason: reason), NoErrors);

        Assert.False(asset.Loading);
    }

    [Fact]
    public void Files_that_broke_the_rules_and_an_unsent_subscription_are_shown()
    {
        var errors = new[] { new ImportLogEntry("BTCUSDT-15m-2026-11-04.csv", 1, 1, 0, "BTCUSDT-15m-2026-11-04.csv, line 7: high is below open or close") };

        var asset = AssetStatusReader.Build(Btc with { SubscriptionSent = false }, NoRanges, ImportProgress.None, null, errors);

        Assert.Equal(new[] { "BTCUSDT-15m-2026-11-04.csv, line 7: high is below open or close" }, asset.Errors);
        Assert.True(asset.SubscriptionPending);
        Assert.False(asset.Loading);
    }

    [Fact]
    public void The_default_history_is_mapped_and_checked()
    {
        var response = SettingsMapping.ToResponse(HistorySettings.Initial, null, "linear");
        var back = SettingsMapping.ToHistory(response.DefaultHistory!);
        var wrong = SettingsMapping.ToHistory(new Dictionary<string, int?> { ["15m"] = 0, ["1h"] = 40000, ["2h"] = 5, ["1d"] = null, ["4h"] = 30 });
        Assert.Equal(new int?[] { 90, 365, 730, null, null }, new[] { "15m", "1h", "4h", "1d", "1w" }.Select(code => response.DefaultHistory![code]));
        Assert.Equal(90, back.Value.DepthOf(Timeframe.M15).Days);
        Assert.True(back.Value.DepthOf(Timeframe.W1).IsComplete);
        Assert.Equal(ErrorType.Validation, wrong.Error.Type);
        Assert.Equal(new[] { "defaultHistory.15m", "defaultHistory.1h", "defaultHistory.2h" }, wrong.Error.Fields.Keys.OrderBy(key => key));
    }

    private static SymbolStatus Downloader(int pendingJobs15m, int pendingJobs1d, string? gapReason = null)
    {
        var gaps = gapReason == null
            ? Array.Empty<CoverageGap>()
            : new[] { new CoverageGap(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), gapReason) };

        return new SymbolStatus(100, true, new[]
        {
            new TimeframeStatus(Timeframe.M15, new DateOnly(2026, 8, 17), null, null, pendingJobs15m, gaps, null),
            new TimeframeStatus(Timeframe.D1, null, null, null, pendingJobs1d, Array.Empty<CoverageGap>(), null)
        });
    }
}
