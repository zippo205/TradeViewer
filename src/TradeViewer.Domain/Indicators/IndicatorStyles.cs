namespace TradeViewer.Domain.Indicators;

/// <summary>The styles a new indicator starts with: TradingView's colours (IN1), width 1.</summary>
public static class IndicatorStyles
{
    /// <summary>The style entry of the histogram columns above zero.</summary>
    public const string HistogramAbove = "histogramAbove";

    /// <summary>The style entry of the histogram columns below zero.</summary>
    public const string HistogramBelow = "histogramBelow";

    private const string Blue = "#2962FF";

    /// <summary>The style entries an indicator of the type has, in this order.</summary>
    public static IReadOnlyList<string> Entries(IndicatorType type) =>
        type == IndicatorType.Macd
            ? new[] { IndicatorLines.Macd, IndicatorLines.Signal, HistogramAbove, HistogramBelow }
            : IndicatorLines.Of(type);

    public static IReadOnlyList<IndicatorLineStyle> Default(IndicatorType type) => type switch
    {
        IndicatorType.Sma or IndicatorType.Ema => new[] { Style(IndicatorLines.Value, Blue) },
        IndicatorType.BollingerBands => new[]
        {
            Style(IndicatorLines.Basis, Blue),
            Style(IndicatorLines.Upper, "#F23645"),
            Style(IndicatorLines.Lower, "#089981")
        },
        IndicatorType.Rsi => new[] { Style(IndicatorLines.Value, "#7E57C2") },
        IndicatorType.Macd => new[]
        {
            Style(IndicatorLines.Macd, Blue),
            Style(IndicatorLines.Signal, "#FF6D00"),
            Style(HistogramAbove, "#26A69A"),
            Style(HistogramBelow, "#FF5252")
        },
        IndicatorType.Atr => new[] { Style(IndicatorLines.Value, "#B71C1C") },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static IndicatorLineStyle Style(string line, string color) => new(line, color, 1);
}
