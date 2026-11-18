namespace TradeViewer.Contracts.Indicators;

/// <summary>
/// The values of one indicator for a page of candles, as column arrays like the candles: T are the open times
/// of the candle page (Unix seconds, UTC), and each line has one value per time, null where the indicator is
/// not defined yet. The lines are named as the indicator names them (value; basis, upper, lower; macd,
/// signal, histogram). Partial: the last value belongs to the current candle (MD8).
/// </summary>
public sealed record IndicatorResponse(long[] T, IReadOnlyDictionary<string, double?[]> Lines, bool Partial);
