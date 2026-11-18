namespace TradeViewer.Domain.Indicators;

/// <summary>
/// One line of a calculated indicator: one value per candle, in the order of the candles; null where
/// the indicator is not defined yet (the start of the history).
/// </summary>
public sealed record IndicatorLine(string Name, double?[] Values);
