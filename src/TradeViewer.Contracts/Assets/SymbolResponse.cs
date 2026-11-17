namespace TradeViewer.Contracts.Assets;

/// <summary>
/// A symbol that can be added (AS2). Symbol is the folder name; SourceSymbol the ticker at the source when it
/// differs (null: the same); Exchange the listing venue, null for crypto and forex. Added: it is an asset of
/// TradeViewer already.
/// </summary>
public sealed record SymbolResponse(
    string Source,
    string Symbol,
    string DisplayName,
    string AssetClass,
    bool Added,
    string? SourceSymbol = null,
    string? Exchange = null);
