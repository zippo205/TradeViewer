namespace TradeViewer.Contracts.Indicators;

/// <summary>The look of one line of an indicator: colour #RRGGBB or #RRGGBBAA, width 1 to 4 pixels.</summary>
public sealed record IndicatorStyleRecord(string Line, string Color, int Width);
