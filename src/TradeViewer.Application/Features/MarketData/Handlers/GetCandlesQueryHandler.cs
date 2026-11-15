using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.MarketData.Queries;
using TradeViewer.Contracts.MarketData;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.MarketData.Handlers;

public sealed class GetCandlesQueryHandler : IQueryHandler<GetCandlesQuery, CandlesResponse>
{
    private readonly IAssetRepository _assets;
    private readonly ICandleStore _store;

    public GetCandlesQueryHandler(IAssetRepository assets, ICandleStore store)
    {
        _assets = assets;
        _store = store;
    }

    public async Task<Result<CandlesResponse>> HandleAsync(GetCandlesQuery query, CancellationToken cancellationToken)
    {
        if (query.Limit != null && (query.Limit < 1 || query.Limit > GetCandlesQuery.MaxLimit))
        {
            return Error.Validation(
                "candles.limit",
                "The limit must be between 1 and " + GetCandlesQuery.MaxLimit + ".",
                new Dictionary<string, string[]> { ["limit"] = new[] { "Must be between 1 and " + GetCandlesQuery.MaxLimit + "." } });
        }

        var asset = await _assets.GetAsync(query.Source, query.Symbol, cancellationToken);
        if (asset == null)
        {
            return Error.NotFound("asset.notFound", "There is no asset " + query.Source + "/" + query.Symbol + ".");
        }

        if (!Timeframes.TryParse(query.Timeframe, out var timeframe) || !asset.History.Has(timeframe))
        {
            return Error.InvalidData("candles.timeframe", query.Source + "/" + query.Symbol + " has no timeframe " + query.Timeframe + ".");
        }

        var series = new SeriesKey(asset.Source, asset.Symbol, timeframe);
        var candles = _store.GetCandles(series, query.To, query.Limit ?? GetCandlesQuery.DefaultLimit);

        // The current candle belongs to the latest page of a timeframe above the smallest one (MD8).
        Candle? current = null;
        var smallest = asset.History.Timeframes[0];
        if (query.To == null && timeframe != smallest)
        {
            long? lastClosed = candles.Count > 0 ? candles[candles.Count - 1].Time : null;
            var from = lastClosed == null ? 0 : Timeframes.CloseTime(timeframe, lastClosed.Value);
            var small = lastClosed == null
                ? _store.GetCandles(new SeriesKey(asset.Source, asset.Symbol, smallest), null, MaxSmallCandles(timeframe, smallest))
                : _store.GetCandlesFrom(new SeriesKey(asset.Source, asset.Symbol, smallest), from);

            current = TimeframeAggregator.CurrentCandle(timeframe, lastClosed, small);
        }

        return Result.Success(ToColumns(candles, current));
    }

    /// <summary>As many small candles as one candle of the timeframe can hold.</summary>
    private static int MaxSmallCandles(Timeframe timeframe, Timeframe smallest)
    {
        return (int)(Timeframes.Seconds(timeframe) / Timeframes.Seconds(smallest));
    }

    private static CandlesResponse ToColumns(IReadOnlyList<Candle> candles, Candle? current)
    {
        var count = candles.Count + (current == null ? 0 : 1);
        var t = new long[count];
        var o = new double[count];
        var h = new double[count];
        var l = new double[count];
        var c = new double[count];
        var v = new double[count];

        for (var index = 0; index < count; index++)
        {
            var candle = index < candles.Count ? candles[index] : current!.Value;
            t[index] = candle.Time;
            o[index] = candle.Open;
            h[index] = candle.High;
            l[index] = candle.Low;
            c[index] = candle.Close;
            v[index] = candle.Volume;
        }

        return new CandlesResponse(t, o, h, l, c, v, current != null);
    }
}
