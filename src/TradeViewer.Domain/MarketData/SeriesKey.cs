namespace TradeViewer.Domain.MarketData;

/// <summary>The candles of one asset in one timeframe. An asset is identified by source and symbol everywhere (decision 0003).</summary>
public readonly record struct SeriesKey(string Source, string Symbol, Timeframe Timeframe);
