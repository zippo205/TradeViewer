using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Infrastructure.MarketData.Store;

/// <summary>Reads pages of candles from the candle store and adds the current candle (see <see cref="ICandleSeriesReader"/>).</summary>
public sealed class CandleSeriesReader : ICandleSeriesReader
{
    private readonly IAssetRepository _assets;
    private readonly ICandleStore _store;

    public CandleSeriesReader(IAssetRepository assets, ICandleStore store)
    {
        _assets = assets;
        _store = store;
    }

    public async Task<Result<CandleSeriesPage>> ReadAsync(
        string source, string symbol, string timeframe, long? to, int? limit, int warmUp, CancellationToken cancellationToken)
    {
        if (limit != null && (limit < 1 || limit > ICandleSeriesReader.MaxLimit))
        {
            return Error.Validation(
                "candles.limit",
                "The limit must be between 1 and " + ICandleSeriesReader.MaxLimit + ".",
                new Dictionary<string, string[]> { ["limit"] = new[] { "Must be between 1 and " + ICandleSeriesReader.MaxLimit + "." } });
        }

        var asset = await _assets.GetAsync(source, symbol, cancellationToken);
        if (asset == null)
        {
            return Error.NotFound("asset.notFound", "There is no asset " + source + "/" + symbol + ".");
        }

        if (!Timeframes.TryParse(timeframe, out var frame) || !asset.History.Has(frame))
        {
            return Error.InvalidData("candles.timeframe", source + "/" + symbol + " has no timeframe " + timeframe + ".");
        }

        var series = new SeriesKey(asset.Source, asset.Symbol, frame);
        var candles = _store.GetCandles(series, to, limit ?? ICandleSeriesReader.DefaultLimit);

        // The current candle belongs to the latest page of a timeframe above the smallest one (MD8).
        Candle? current = null;
        var smallest = asset.History.Timeframes[0];
        if (to == null && frame != smallest)
        {
            long? lastClosed = candles.Count > 0 ? candles[candles.Count - 1].Time : null;
            var from = lastClosed == null ? 0 : Timeframes.CloseTime(frame, lastClosed.Value);
            var small = lastClosed == null
                ? _store.GetCandles(new SeriesKey(asset.Source, asset.Symbol, smallest), null, MaxSmallCandles(frame, smallest))
                : _store.GetCandlesFrom(new SeriesKey(asset.Source, asset.Symbol, smallest), from);

            current = TimeframeAggregator.CurrentCandle(frame, lastClosed, small);
        }

        // The candles before the page, for a calculation that looks back (IN1).
        IReadOnlyList<Candle> before = warmUp > 0 && candles.Count > 0
            ? _store.GetCandles(series, candles[0].Time, warmUp)
            : Array.Empty<Candle>();

        var all = new List<Candle>(before.Count + candles.Count + 1);
        all.AddRange(before);
        all.AddRange(candles);
        if (current != null)
        {
            all.Add(current.Value);
        }

        return Result.Success(new CandleSeriesPage(all, before.Count, current != null));
    }

    /// <summary>As many small candles as one candle of the timeframe can hold.</summary>
    private static int MaxSmallCandles(Timeframe timeframe, Timeframe smallest)
    {
        return (int)(Timeframes.Seconds(timeframe) / Timeframes.Seconds(smallest));
    }
}
