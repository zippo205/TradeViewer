namespace TradeViewer.Domain.Indicators;

/// <summary>The names of the lines of each indicator, in the order the legend shows them.</summary>
public static class IndicatorLines
{
    public const string Value = "value";
    public const string Basis = "basis";
    public const string Upper = "upper";
    public const string Lower = "lower";
    public const string Macd = "macd";
    public const string Signal = "signal";
    public const string Histogram = "histogram";

    public static IReadOnlyList<string> Of(IndicatorType type) => type switch
    {
        IndicatorType.Sma or IndicatorType.Ema or IndicatorType.Rsi or IndicatorType.Atr => new[] { Value },
        IndicatorType.BollingerBands => new[] { Basis, Upper, Lower },
        IndicatorType.Macd => new[] { Macd, Signal, Histogram },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
