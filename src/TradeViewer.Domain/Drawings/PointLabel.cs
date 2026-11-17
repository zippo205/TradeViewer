namespace TradeViewer.Domain.Drawings;

/// <summary>A wave label on a point of a polyline (DR5). Point is the index of the anchor, 0 for the first.</summary>
public sealed record PointLabel(int Point, WaveMark Mark);
