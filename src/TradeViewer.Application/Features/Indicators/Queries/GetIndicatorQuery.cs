using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Indicators;

namespace TradeViewer.Application.Features.Indicators.Queries;

/// <summary>
/// One indicator for the page of candles that To and Limit name (the same as GetCandlesQuery). Type is the code
/// (sma, ema, bb, rsi, macd, atr); the parameters the type has may be left out and then take the defaults of
/// IN1; Price is the code of the price source (open ... ohlc4).
/// </summary>
public sealed record GetIndicatorQuery(
    string Source,
    string Symbol,
    string Timeframe,
    string? Type,
    int? Length,
    double? Deviation,
    int? Fast,
    int? Slow,
    int? Signal,
    string? Price,
    long? To,
    int? Limit) : IQuery<IndicatorResponse>;
