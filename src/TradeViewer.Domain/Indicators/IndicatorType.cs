namespace TradeViewer.Domain.Indicators;

/// <summary>The indicators TradeViewer calculates (IN1): three on the price, three in their own pane.</summary>
public enum IndicatorType
{
    Sma,
    Ema,
    BollingerBands,
    Rsi,
    Macd,
    Atr
}
