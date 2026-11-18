namespace TradeViewer.Contracts.Indicators;

/// <summary>
/// One indicator on the charts of an asset or in a template (IN1, IN2). Type is the code (sma, ema, bb, rsi,
/// macd, atr) and says which parameters are given, named as in GET /api/indicators: length (sma, ema, bb,
/// rsi, atr), deviation (bb), fast, slow and signal (macd), price (all but atr: open, high, low, close, hl2,
/// hlc3, ohlc4). The others are null. Styles: one per line of the indicator, MACD has histogramAbove and
/// histogramBelow instead of histogram. Id is a UUID the client makes.
/// </summary>
public sealed record IndicatorRecord(
    Guid Id,
    string Type,
    int? Length,
    double? Deviation,
    int? Fast,
    int? Slow,
    int? Signal,
    string? Price,
    IReadOnlyList<IndicatorStyleRecord> Styles,
    bool Hidden);
