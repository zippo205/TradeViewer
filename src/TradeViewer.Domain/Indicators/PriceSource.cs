namespace TradeViewer.Domain.Indicators;

/// <summary>The price of a candle an indicator is calculated from (IN1), as TradingView offers them.</summary>
public enum PriceSource
{
    Open,
    High,
    Low,
    Close,

    /// <summary>(high + low) / 2</summary>
    Hl2,

    /// <summary>(high + low + close) / 3</summary>
    Hlc3,

    /// <summary>(open + high + low + close) / 4</summary>
    Ohlc4
}
