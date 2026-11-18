using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>
/// Reads a page of candles of an asset exactly as the chart shows it: the newest candles that open before
/// <c>to</c> (Unix seconds, exclusive; the latest without it), at most <c>limit</c>, and on the latest page of a
/// timeframe above the smallest one the current candle built from the smallest (MD8). Used by the candles
/// (CH4) and the indicators (IN1), so an indicator has one value for each candle the chart has.
/// </summary>
public interface ICandleSeriesReader
{
    /// <summary>Candles of a page when the request names no limit.</summary>
    const int DefaultLimit = 2000;

    /// <summary>The largest page.</summary>
    const int MaxLimit = 5000;

    /// <summary>
    /// The page with up to <paramref name="warmUp"/> candles before it. Errors: an unknown asset (not found),
    /// a timeframe the asset does not have (invalid data), a limit outside 1 to <see cref="MaxLimit"/> (validation).
    /// </summary>
    Task<Result<CandleSeriesPage>> ReadAsync(
        string source, string symbol, string timeframe, long? to, int? limit, int warmUp, CancellationToken cancellationToken);
}
