namespace TradeViewer.Contracts.MarketData;

/// <summary>
/// Candles as column arrays, oldest first: open times in Unix seconds (UTC), open, high, low, close, volume.
/// Partial: the last element is the current candle, still in progress and built by TradeViewer (MD8).
/// </summary>
public sealed record CandlesResponse(long[] T, double[] O, double[] H, double[] L, double[] C, double[] V, bool Partial);
