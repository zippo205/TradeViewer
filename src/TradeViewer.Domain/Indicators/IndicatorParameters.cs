namespace TradeViewer.Domain.Indicators;

/// <summary>
/// The parameters of one indicator. Each type has its own record, so a parameter exists only where it
/// means something (ATR has no price source, only MACD has three lengths). <see cref="IndicatorRules"/>
/// says which values are allowed, <see cref="Default"/> gives the defaults of TradingView (IN1).
/// </summary>
public abstract record IndicatorParameters
{
    public abstract IndicatorType Type { get; }

    /// <summary>The parameters a new indicator of the type starts with (IN1).</summary>
    public static IndicatorParameters Default(IndicatorType type) => type switch
    {
        IndicatorType.Sma => new SmaParameters(9, PriceSource.Close),
        IndicatorType.Ema => new EmaParameters(9, PriceSource.Close),
        IndicatorType.BollingerBands => new BollingerBandsParameters(20, 2, PriceSource.Close),
        IndicatorType.Rsi => new RsiParameters(14, PriceSource.Close),
        IndicatorType.Macd => new MacdParameters(12, 26, 9, PriceSource.Close),
        IndicatorType.Atr => new AtrParameters(14),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

/// <summary>Simple moving average: the mean of the last <see cref="Length"/> prices.</summary>
public sealed record SmaParameters(int Length, PriceSource Source) : IndicatorParameters
{
    public override IndicatorType Type => IndicatorType.Sma;
}

/// <summary>Exponential moving average over <see cref="Length"/> prices.</summary>
public sealed record EmaParameters(int Length, PriceSource Source) : IndicatorParameters
{
    public override IndicatorType Type => IndicatorType.Ema;
}

/// <summary>Bollinger Bands: an SMA of <see cref="Length"/> prices, the bands <see cref="Deviation"/> standard deviations away.</summary>
public sealed record BollingerBandsParameters(int Length, double Deviation, PriceSource Source) : IndicatorParameters
{
    public override IndicatorType Type => IndicatorType.BollingerBands;
}

/// <summary>Relative strength index over <see cref="Length"/> changes of the price.</summary>
public sealed record RsiParameters(int Length, PriceSource Source) : IndicatorParameters
{
    public override IndicatorType Type => IndicatorType.Rsi;
}

/// <summary>MACD: EMA(<see cref="Fast"/>) - EMA(<see cref="Slow"/>), with a signal line EMA(<see cref="Signal"/>) of it.</summary>
public sealed record MacdParameters(int Fast, int Slow, int Signal, PriceSource Source) : IndicatorParameters
{
    public override IndicatorType Type => IndicatorType.Macd;
}

/// <summary>Average true range over <see cref="Length"/> candles; it uses high, low and close, so it has no price source.</summary>
public sealed record AtrParameters(int Length) : IndicatorParameters
{
    public override IndicatorType Type => IndicatorType.Atr;
}
