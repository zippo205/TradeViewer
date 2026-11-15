namespace TradeViewer.Contracts.Assets;

/// <summary>Body of POST /api/assets: a symbol of the list of its source (AS2).</summary>
public sealed record AddAssetRequest(string Source, string Symbol);
