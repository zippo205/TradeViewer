namespace TradeViewer.Domain.Indicators;

/// <summary>
/// The look of one line of an indicator (IN1): colour #RRGGBB or #RRGGBBAA, width 1 to 4 pixels. The
/// MACD histogram has two entries, <see cref="IndicatorStyles.HistogramAbove"/> and
/// <see cref="IndicatorStyles.HistogramBelow"/>; the chart draws the falling columns of each in a lighter shade.
/// </summary>
public sealed record IndicatorLineStyle(string Line, string Color, int Width);
