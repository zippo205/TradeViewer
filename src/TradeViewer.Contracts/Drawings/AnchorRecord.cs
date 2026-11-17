namespace TradeViewer.Contracts.Drawings;

/// <summary>A point of a drawing: time (UTC) and price. Price is null only for a wave label.</summary>
public sealed record AnchorRecord(DateTimeOffset Time, double? Price);
