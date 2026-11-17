namespace TradeViewer.Contracts.Assets;

/// <summary>
/// Body of POST /api/assets: a symbol picked from its source (AS2). SourceSymbol is the ticker the user picked from
/// the catalog when it differs from the symbol; the Downloader's lookup by that ticker gives the folder name.
/// </summary>
public sealed record AddAssetRequest(string Source, string Symbol, string? SourceSymbol = null);
