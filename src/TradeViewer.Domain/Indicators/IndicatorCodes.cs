namespace TradeViewer.Domain.Indicators;

/// <summary>The codes of indicator types and price sources as the API and the stored records write them.</summary>
public static class IndicatorCodes
{
    private static readonly (IndicatorType Value, string Code)[] Types =
    {
        (IndicatorType.Sma, "sma"),
        (IndicatorType.Ema, "ema"),
        (IndicatorType.BollingerBands, "bb"),
        (IndicatorType.Rsi, "rsi"),
        (IndicatorType.Macd, "macd"),
        (IndicatorType.Atr, "atr")
    };

    private static readonly (PriceSource Value, string Code)[] Sources =
    {
        (PriceSource.Open, "open"),
        (PriceSource.High, "high"),
        (PriceSource.Low, "low"),
        (PriceSource.Close, "close"),
        (PriceSource.Hl2, "hl2"),
        (PriceSource.Hlc3, "hlc3"),
        (PriceSource.Ohlc4, "ohlc4")
    };

    public static string Code(IndicatorType type) => CodeOf(Types, type);

    public static string Code(PriceSource source) => CodeOf(Sources, source);

    public static bool TryParse(string? code, out IndicatorType type) => TryParseOf(Types, code, out type);

    public static bool TryParse(string? code, out PriceSource source) => TryParseOf(Sources, code, out source);

    private static string CodeOf<T>((T Value, string Code)[] table, T value)
        where T : struct, Enum
    {
        foreach (var pair in table)
        {
            if (EqualityComparer<T>.Default.Equals(pair.Value, value))
            {
                return pair.Code;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static bool TryParseOf<T>((T Value, string Code)[] table, string? code, out T value)
        where T : struct, Enum
    {
        foreach (var pair in table)
        {
            if (pair.Code == code)
            {
                value = pair.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
