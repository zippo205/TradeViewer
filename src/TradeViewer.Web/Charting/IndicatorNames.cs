namespace TradeViewer.Web.Charting;

/// <summary>How the indicators and their lines are named on the page, as TradingView names them (IN1).</summary>
public static class IndicatorNames
{
    /// <summary>The kinds in the order the menu offers them.</summary>
    public static IReadOnlyList<(string Type, string Name)> Kinds { get; } = new[]
    {
        ("sma", "Moving Average Simple (SMA)"),
        ("ema", "Moving Average Exponential (EMA)"),
        ("bb", "Bollinger Bands"),
        ("rsi", "Relative Strength Index (RSI)"),
        ("macd", "MACD"),
        ("atr", "Average True Range (ATR)")
    };

    /// <summary>The price sources as the Api writes them (open ... ohlc4).</summary>
    public static IReadOnlyList<string> Prices { get; } = new[] { "open", "high", "low", "close", "hl2", "hlc3", "ohlc4" };

    public static string Name(string type) => Kinds.FirstOrDefault(kind => kind.Type == type).Name ?? type;

    /// <summary>The name of a style entry in the settings dialog.</summary>
    public static string Line(string type, string line) => line switch
    {
        "value" => type.ToUpperInvariant(),
        "basis" => "Basis",
        "upper" => "Upper",
        "lower" => "Lower",
        "macd" => "MACD",
        "signal" => "Signal",
        "histogramAbove" => "Histogram above zero",
        "histogramBelow" => "Histogram below zero",
        _ => line
    };
}
