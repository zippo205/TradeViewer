using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>What the candle store holds of one timeframe of an asset: the open times of its first and last candle (Unix seconds).</summary>
public sealed record SeriesRange(Timeframe Timeframe, long FirstTime, long LastTime);
