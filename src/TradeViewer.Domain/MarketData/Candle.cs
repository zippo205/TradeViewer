namespace TradeViewer.Domain.MarketData;

/// <summary>One candle as the Downloader delivered it (MD1). <see cref="Time"/> is the open time in Unix seconds, UTC.</summary>
public readonly record struct Candle(long Time, double Open, double High, double Low, double Close, double Volume);
