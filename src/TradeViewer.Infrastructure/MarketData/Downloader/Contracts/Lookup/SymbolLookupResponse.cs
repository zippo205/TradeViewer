// Copied from Downloader.Contracts (Downloader repository). Do not edit; copy again on changes.

namespace Downloader.Contracts.Lookup;

/// <summary>GET /api/v1/sources/{source}/symbols/{sourceSymbol}: one ticker, without subscribing.</summary>
public sealed class SymbolLookupResponse
{
    public string Source { get; set; } = string.Empty;

    /// <summary>The folder name the Downloader suggests for subscribing, e.g. <c>EURUSD</c>.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>The ticker at the source, e.g. <c>EURUSD=X</c>.</summary>
    public string SourceSymbol { get; set; } = string.Empty;

    /// <summary>crypto, stock, etf, forex</summary>
    public string AssetClass { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public decimal? TickSize { get; set; }

    public string? ExchangeTimezone { get; set; }

    public SessionResponse Session { get; set; } = new SessionResponse();

    public List<string> SourceTimeframes { get; set; } = new List<string>();

    /// <summary>Earliest available date per timeframe code.</summary>
    public Dictionary<string, DateOnly> Earliest { get; set; } = new Dictionary<string, DateOnly>();
}

/// <summary>As in symbols.json (docs/specification.md, Session).</summary>
public sealed class SessionResponse
{
    /// <summary>continuous, exchange, newYorkClose</summary>
    public string Type { get; set; } = string.Empty;

    public string TimeZone { get; set; } = string.Empty;

    /// <summary>HH:mm local time in <see cref="TimeZone"/>; exchange sessions only.</summary>
    public string? Open { get; set; }

    /// <summary>HH:mm local time in <see cref="TimeZone"/>.</summary>
    public string? Close { get; set; }
}
