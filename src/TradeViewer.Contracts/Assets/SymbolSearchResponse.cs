namespace TradeViewer.Contracts.Assets;

/// <summary>
/// The answer of GET /api/symbols/{source}?q= (AS2). Total counts all matches; Truncated: more matched than Symbols
/// holds, the user types more. FromCatalog: the Downloader's catalog answered (false: the fixed list, without the
/// Downloader). RefreshedAt: when the catalog was refreshed; null with FromCatalog means it is not loaded yet.
/// </summary>
public sealed record SymbolSearchResponse(
    IReadOnlyList<SymbolResponse> Symbols,
    int Total,
    bool Truncated,
    bool FromCatalog,
    DateTimeOffset? RefreshedAt = null);
