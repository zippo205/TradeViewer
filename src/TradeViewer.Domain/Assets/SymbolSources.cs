namespace TradeViewer.Domain.Assets;

/// <summary>
/// The sources whose symbols can be added (AS2): those TradeViewer can chart. binance writes the Binance spot format
/// with a continuous session; yahoo and tradingview, which the Downloader's catalog also lists, write the header
/// format with trading sessions and follow with the epic for stocks, ETFs and forex (roadmap, Later).
/// </summary>
public static class SymbolSources
{
    public static IReadOnlyList<string> Chartable { get; } = new[] { "binance" };

    public static bool IsChartable(string? source)
    {
        return source != null && Chartable.Contains(source);
    }
}
