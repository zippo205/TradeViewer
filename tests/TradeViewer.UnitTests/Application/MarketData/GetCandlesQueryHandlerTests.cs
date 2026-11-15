using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.MarketData.Handlers;
using TradeViewer.Application.Features.MarketData.Queries;
using TradeViewer.Contracts.MarketData;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.UnitTests.Application.Assets;

namespace TradeViewer.UnitTests.Application.MarketData;

/// <summary>The candle endpoint at the level of its handler: pages (CH4), the current candle (MD8), errors.</summary>
public class GetCandlesQueryHandlerTests
{
    private const long Start = 1762214400; // 2025-11-04 00:00 UTC

    private readonly FakeAssetRepository _assets = new FakeAssetRepository();
    private readonly FakeCandleStore _store = new FakeCandleStore();

    public GetCandlesQueryHandlerTests()
    {
        var info = new SymbolInfo("binance", "BTCUSDT", "BTC/USDT", AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
        var history = new AssetHistory(new Dictionary<Timeframe, DateOnly?> { [Timeframe.M15] = null, [Timeframe.H1] = null, [Timeframe.D1] = null });
        _assets.Assets.Add(new Asset(info, history, 1, true));

        // 15m candles from 00:00 to 02:30 (11 candles); 1h candles of 00:00 and 01:00: the hour of 02:00 is in progress.
        _store.Candles[new SeriesKey("binance", "BTCUSDT", Timeframe.M15)] = Enumerable.Range(0, 11)
            .Select(index => new Candle(Start + index * 900, 100 + index, 102 + index, 99 + index, 101 + index, 10))
            .ToList();
        _store.Candles[new SeriesKey("binance", "BTCUSDT", Timeframe.H1)] = new List<Candle>
        {
            new Candle(Start, 100, 105, 99, 104, 40),
            new Candle(Start + 3600, 104, 109, 103, 108, 40)
        };
    }

    [Fact]
    public async Task The_smallest_timeframe_is_returned_as_columns_without_a_current_candle()
    {
        var page = (await GetAsync("15m", null, null)).Value;

        Assert.False(page.Partial);
        Assert.Equal(11, page.T.Length);
        Assert.Equal(Start, page.T[0]);
        Assert.Equal(Start + 10 * 900, page.T[10]);
        Assert.Equal(new[] { 100.0, 102.0, 99.0, 101.0, 10.0 }, new[] { page.O[0], page.H[0], page.L[0], page.C[0], page.V[0] });
        Assert.All(new[] { page.O.Length, page.H.Length, page.L.Length, page.C.Length, page.V.Length }, length => Assert.Equal(11, length));
    }

    [Fact]
    public async Task A_larger_timeframe_ends_with_the_current_candle_built_from_the_smallest_one()
    {
        var page = (await GetAsync("1h", null, null)).Value;

        Assert.True(page.Partial);
        Assert.Equal(new[] { Start, Start + 3600, Start + 7200 }, page.T);

        // 02:00, 02:15, 02:30: open of the first, high and low over all, close of the last, volume summed.
        Assert.Equal(new[] { 108.0, 112.0, 107.0, 111.0, 30.0 }, new[] { page.O[2], page.H[2], page.L[2], page.C[2], page.V[2] });
    }

    [Fact]
    public async Task The_current_candle_is_gone_as_soon_as_the_Downloader_delivered_the_closed_one()
    {
        _store.Candles[new SeriesKey("binance", "BTCUSDT", Timeframe.H1)].Add(new Candle(Start + 7200, 108, 113, 107, 112, 40));

        var page = (await GetAsync("1h", null, null)).Value;

        Assert.False(page.Partial);
        Assert.Equal(3, page.T.Length);
        Assert.Equal(112, page.C[2]);
    }

    [Fact]
    public async Task A_timeframe_without_closed_candles_shows_the_current_one_alone()
    {
        var page = (await GetAsync("1d", null, null)).Value;

        Assert.True(page.Partial);
        Assert.Equal(new[] { Start }, page.T);
        Assert.Equal(new[] { 100.0, 112.0, 99.0, 111.0, 110.0 }, new[] { page.O[0], page.H[0], page.L[0], page.C[0], page.V[0] });
    }

    [Fact]
    public async Task Older_pages_hold_the_candles_before_a_time_and_never_a_current_candle()
    {
        var latest = (await GetAsync("15m", null, 4)).Value;
        var older = (await GetAsync("15m", latest.T[0], 4)).Value;
        var oldest = (await GetAsync("15m", older.T[0], 4)).Value;
        var none = (await GetAsync("15m", oldest.T[0], 4)).Value;
        var olderHours = (await GetAsync("1h", Start + 3600, null)).Value;

        Assert.Equal(new[] { 7, 8, 9, 10 }.Select(index => Start + index * 900L), latest.T);
        Assert.Equal(new[] { 3, 4, 5, 6 }.Select(index => Start + index * 900L), older.T);
        Assert.Equal(new[] { 0, 1, 2 }.Select(index => Start + index * 900L), oldest.T);
        Assert.Empty(none.T);
        Assert.Equal(new[] { Start }, olderHours.T);
        Assert.False(olderHours.Partial);
    }

    [Fact]
    public async Task Wrong_requests_are_errors()
    {
        var unknownAsset = await new GetCandlesQueryHandler(_assets, _store)
            .HandleAsync(new GetCandlesQuery("binance", "ETHUSDT", "15m", null, null), CancellationToken.None);
        var notOfTheAsset = await GetAsync("4h", null, null);
        var noTimeframe = await GetAsync("7m", null, null);
        var tooMany = await GetAsync("15m", null, 5001);
        var tooFew = await GetAsync("15m", null, 0);

        Assert.Equal(ErrorType.NotFound, unknownAsset.Error.Type);
        Assert.Equal(ErrorType.InvalidData, notOfTheAsset.Error.Type);
        Assert.Equal(ErrorType.InvalidData, noTimeframe.Error.Type);
        Assert.Equal(ErrorType.Validation, tooMany.Error.Type);
        Assert.Equal(ErrorType.Validation, tooFew.Error.Type);
        Assert.True((await GetAsync("15m", null, 5000)).IsSuccess);
    }

    private Task<Result<CandlesResponse>> GetAsync(string timeframe, long? to, int? limit)
    {
        return new GetCandlesQueryHandler(_assets, _store)
            .HandleAsync(new GetCandlesQuery("binance", "BTCUSDT", timeframe, to, limit), CancellationToken.None);
    }
}
