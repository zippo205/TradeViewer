namespace TradeViewer.Contracts.Overlays;

/// <summary>A point of a line shape: time (UTC) and price.</summary>
public sealed record OverlayPointRecord(DateTimeOffset Time, double Price);
