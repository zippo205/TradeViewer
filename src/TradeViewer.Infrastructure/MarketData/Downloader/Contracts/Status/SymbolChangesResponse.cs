// Copied from Downloader.Contracts (Downloader repository). Do not edit; copy again on changes.

namespace Downloader.Contracts.Status;

/// <summary>GET /api/v1/status/{source}/{symbol}/changes?after={n}</summary>
public sealed class SymbolChangesResponse
{
    public long LastChange { get; set; }

    /// <summary>False: changes may have been purged (older than 30 days); rescan the symbol.</summary>
    public bool Complete { get; set; }

    public List<ChangeResponse> Changes { get; set; } = new List<ChangeResponse>();
}

public sealed class ChangeResponse
{
    public long Change { get; set; }

    public string Timeframe { get; set; } = string.Empty;

    /// <summary>Below the symbol folder, e.g. <c>15m/BTCUSDT-15m-2026-11-04.csv</c>.</summary>
    public string File { get; set; } = string.Empty;

    /// <summary>new, updated, replaced</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>UTC</summary>
    public DateTime At { get; set; }
}
