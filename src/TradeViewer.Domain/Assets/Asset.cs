namespace TradeViewer.Domain.Assets;

/// <summary>
/// An asset of TradeViewer: a symbol of a source, the history wanted of it and its place in its watchlist.
/// SubscriptionSent false: the Downloader has not accepted the current history yet.
/// </summary>
public sealed record Asset(SymbolInfo Info, AssetHistory History, int Position, bool SubscriptionSent)
{
    public string Source => Info.Source;

    public string Symbol => Info.Symbol;
}
