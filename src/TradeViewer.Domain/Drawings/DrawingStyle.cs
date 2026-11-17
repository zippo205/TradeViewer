namespace TradeViewer.Domain.Drawings;

/// <summary>
/// The look of a drawing (DR2). Colours are written #RRGGBB or #RRGGBBAA. Width is the line width in pixels,
/// 1 to 4. Fill is the colour of the area of a rectangle or a parallel channel, null for no fill.
/// </summary>
public sealed record DrawingStyle(string Color, int Width, LineStyle LineStyle, string? Fill = null);
