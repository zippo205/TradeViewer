namespace TradeViewer.Contracts.Assets;

/// <summary>A symbol that can be added (AS2). Added: it is an asset of TradeViewer already.</summary>
public sealed record SymbolResponse(string Source, string Symbol, string DisplayName, string AssetClass, bool Added);
