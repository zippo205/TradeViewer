namespace TradeViewer.Contracts.Drawings;

/// <summary>Colour and fill as #RRGGBB or #RRGGBBAA, width 1 to 4, line style solid, dashed or dotted.</summary>
public sealed record DrawingStyleRecord(string Color, int Width, string LineStyle, string? Fill = null);
