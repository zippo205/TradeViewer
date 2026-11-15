namespace TradeViewer.Domain.MarketData;

/// <summary>The timeframes of TradeViewer. Their codes (15m, 1h, 4h, 1d, 1w) are the same everywhere: files, API, Downloader.</summary>
public enum Timeframe
{
    M15,
    H1,
    H4,
    D1,
    W1
}
