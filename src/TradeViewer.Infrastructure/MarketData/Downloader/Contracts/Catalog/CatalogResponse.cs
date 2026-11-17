// Copied from Downloader.Contracts (Downloader repository). Do not edit; copy again on changes.

namespace Downloader.Contracts.Catalog;

/// <summary>GET /api/v1/sources/{source}/symbols?q=&amp;assetClass=&amp;limit=: the symbols a source offers.</summary>
public sealed class CatalogResponse
{
    public string Source { get; set; } = string.Empty;

    /// <summary>UTC; null if the source has no list yet (just started, or the first refresh failed).</summary>
    public DateTime? RefreshedAt { get; set; }

    /// <summary>All matches; can be more than <see cref="Symbols"/> holds.</summary>
    public int Total { get; set; }

    /// <summary>True when more matches exist than were returned (raise <c>limit</c> or narrow <c>q</c>).</summary>
    public bool Truncated { get; set; }

    public List<CatalogSymbolResponse> Symbols { get; set; } = new List<CatalogSymbolResponse>();
}

public sealed class CatalogSymbolResponse
{
    /// <summary>The ticker for subscribe and lookup, e.g. <c>BTCUSDT</c>, <c>BRK-B</c>, <c>NYSE:BRK.B</c>.</summary>
    public string SourceSymbol { get; set; } = string.Empty;

    /// <summary>The folder name the Downloader suggests, e.g. <c>BRK-B</c>.</summary>
    public string Symbol { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>crypto, stock, etf, forex</summary>
    public string AssetClass { get; set; } = string.Empty;

    /// <summary>The listing venue; null for crypto and forex.</summary>
    public string? Exchange { get; set; }
}
