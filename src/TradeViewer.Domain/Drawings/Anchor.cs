namespace TradeViewer.Domain.Drawings;

/// <summary>
/// A point of a drawing: a time (Unix seconds, UTC) and a price, never a bar index, so the drawing stays
/// where it is on every timeframe (DR3). The time may lie after the last candle.
/// Price is null only for a wave label, which is drawn at the high or low of the candle that contains its
/// time (DR5).
/// </summary>
public readonly record struct Anchor(long Time, double? Price);
