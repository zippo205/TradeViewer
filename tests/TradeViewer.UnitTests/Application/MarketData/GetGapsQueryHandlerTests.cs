using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.MarketData.Handlers;
using TradeViewer.Application.Features.MarketData.Queries;
using TradeViewer.Contracts.MarketData;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.UnitTests.Application.Assets;

namespace TradeViewer.UnitTests.Application.MarketData;

/// <summary>MD4: which gaps the chart is told about.</summary>
public class GetGapsQueryHandlerTests
{
    private static readonly DateTimeOffset Outage = new DateTimeOffset(2021, 8, 13, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeAssetRepository _assets = new FakeAssetRepository();
    private readonly FakeCandleStore _store = new FakeCandleStore();

    [Fact]
    public async Task Gaps_the_source_confirmed_are_returned_oldest_first_and_nothing_else()
    {
        AddAsset(null);
        Status(
            Gap(Outage.AddYears(2), 1, "missingUpstream"),
            Gap(Outage, 4.5, "missingUpstream"),
            Gap(Outage.AddYears(1), 3, "pending"),       // still being downloaded: loading, not a gap
            Gap(Outage.AddYears(-3), 100, "unavailable")); // before the history limit of the source

        var gaps = (await GetAsync("15m")).Value;

        Assert.Equal(
            new[]
            {
                new GapResponse(Outage.ToUnixTimeSeconds(), Outage.AddHours(4.5).ToUnixTimeSeconds(), "missingUpstream"),
                new GapResponse(Outage.AddYears(2).ToUnixTimeSeconds(), Outage.AddYears(2).AddHours(1).ToUnixTimeSeconds(), "missingUpstream")
            },
            gaps);
    }

    [Fact]
    public async Task A_gap_before_the_history_of_the_asset_is_not_its_gap()
    {
        // The Downloader holds candles from 2019 for another client; this asset wants them from 2026 only.
        AddAsset(new DateOnly(2026, 8, 17));
        Status(Gap(Outage, 4.5, "missingUpstream"), Gap(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), 2, "missingUpstream"));

        var gaps = (await GetAsync("15m")).Value;

        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), Assert.Single(gaps).From);
    }

    [Fact]
    public async Task Every_timeframe_has_its_own_gaps()
    {
        AddAsset(null);
        Status(Gap(Outage, 4.5, "missingUpstream"));

        Assert.Single((await GetAsync("15m")).Value);
        Assert.Empty((await GetAsync("1d")).Value); // the Downloader reported none for the daily candles
    }

    [Fact]
    public async Task Without_a_status_of_the_Downloader_no_gaps_are_known()
    {
        AddAsset(null);

        Assert.Empty((await GetAsync("15m")).Value);
    }

    [Fact]
    public async Task An_unknown_asset_and_a_timeframe_the_asset_does_not_have_are_errors()
    {
        var unknown = await GetAsync("15m");
        AddAsset(null);
        var noTimeframe = await GetAsync("4h");

        Assert.Equal(ErrorType.NotFound, unknown.Error.Type);
        Assert.Equal(ErrorType.InvalidData, noTimeframe.Error.Type);
    }

    private void AddAsset(DateOnly? from15m)
    {
        var info = new SymbolInfo("binance", "BTCUSDT", "BTC/USDT", AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
        var history = new AssetHistory(new Dictionary<Timeframe, DateOnly?> { [Timeframe.M15] = from15m, [Timeframe.D1] = null });
        _assets.Assets.Add(new Asset(info, history, 1, true));
    }

    private void Status(params CoverageGap[] gaps15m)
    {
        var status = new SymbolStatus(1, true, new[]
        {
            new TimeframeStatus(Timeframe.M15, null, null, null, 0, gaps15m, null),
            new TimeframeStatus(Timeframe.D1, null, null, null, 0, Array.Empty<CoverageGap>(), null)
        });
        _store.SyncStates[("binance", "BTCUSDT")] = new SyncState(1, status, 1);
    }

    private static CoverageGap Gap(DateTimeOffset from, double hours, string reason)
    {
        return new CoverageGap(from, from.AddHours(hours), reason);
    }

    private Task<Result<IReadOnlyList<GapResponse>>> GetAsync(string timeframe)
    {
        return new GetGapsQueryHandler(_assets, _store).HandleAsync(new GetGapsQuery("binance", "BTCUSDT", timeframe), CancellationToken.None);
    }
}
