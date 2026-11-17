namespace TradeViewer.Contracts.Drawings;

/// <summary>A wave label on a point of a polyline; Point is the index of the anchor, 0 for the first.</summary>
public sealed record PointLabelRecord(int Point, WaveMarkRecord Mark);
