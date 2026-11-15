// Copied from Downloader.Contracts (Downloader repository). Do not edit; copy again on changes.

namespace Downloader.Contracts.Status;

/// <summary>GET /api/v1/status/{source}/{symbol}</summary>
public sealed class SymbolStatusResponse
{
    public string Source { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public List<string> SubscribedBy { get; set; } = new List<string>();

    /// <summary>Highest change number of the symbol; poll it and fetch /changes when it increased.</summary>
    public long LastChange { get; set; }

    public List<TimeframeStatusResponse> Timeframes { get; set; } = new List<TimeframeStatusResponse>();
}

public sealed class TimeframeStatusResponse
{
    public string Timeframe { get; set; } = string.Empty;

    /// <summary>yyyy-MM-dd; null if not subscribed or complete history with an unknown first date.</summary>
    public DateOnly? WantedFrom { get; set; }

    /// <summary>UTC</summary>
    public DateTime? CoveredFrom { get; set; }

    /// <summary>UTC, exclusive: the end of the last covered candle.</summary>
    public DateTime? CoveredTo { get; set; }

    public long? LastChange { get; set; }

    /// <summary>UTC</summary>
    public DateTime? LastChangedAt { get; set; }

    /// <summary>Below the symbol folder, e.g. <c>15m/BTCUSDT-15m-2026-11-04.csv</c>.</summary>
    public string? LatestFile { get; set; }

    public int PendingJobs { get; set; }

    public List<GapResponse> Gaps { get; set; } = new List<GapResponse>();

    public string? LastError { get; set; }
}

public sealed class GapResponse
{
    /// <summary>UTC</summary>
    public DateTime From { get; set; }

    /// <summary>UTC, exclusive</summary>
    public DateTime To { get; set; }

    /// <summary>pending, missingUpstream, unavailable</summary>
    public string Reason { get; set; } = string.Empty;
}
