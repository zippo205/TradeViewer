// Copied from Downloader.Contracts (Downloader repository). Do not edit; copy again on changes.

namespace Downloader.Contracts.Status;

/// <summary>GET /api/v1/status. Queue and sources are added by the Status epic.</summary>
public sealed class ServiceStatusResponse
{
    /// <summary><c>running</c></summary>
    public string State { get; set; } = string.Empty;

    /// <summary>UTC; serialised as ISO 8601 with <c>Z</c>.</summary>
    public DateTime StartedAt { get; set; }

    public string Version { get; set; } = string.Empty;

    /// <summary>The most recent update cycle (null before the first one).</summary>
    public CycleResponse? LastCycle { get; set; }

    /// <summary>UTC; null if update cycles are disabled.</summary>
    public DateTime? NextCycleAt { get; set; }

    public QueueResponse Queue { get; set; } = new QueueResponse();

    public List<SourceStatusResponse> Sources { get; set; } = new List<SourceStatusResponse>();

    /// <summary>The last weekly consistency check of the files (decision 0006); null before the first one.</summary>
    public ConsistencyCheckResponse? ConsistencyCheck { get; set; }

    /// <summary>The symbol catalog per source (GET /api/v1/sources/{source}/symbols); sources without a list are not listed.</summary>
    public List<CatalogStatusResponse> Catalogs { get; set; } = new List<CatalogStatusResponse>();
}

public sealed class CatalogStatusResponse
{
    public string Source { get; set; } = string.Empty;

    /// <summary>UTC; null if no refresh has succeeded yet.</summary>
    public DateTime? RefreshedAt { get; set; }

    public int SymbolCount { get; set; }

    /// <summary>The last failed refresh; cleared by the next successful one.</summary>
    public string? LastError { get; set; }

    /// <summary>UTC</summary>
    public DateTime? LastErrorAt { get; set; }
}

public sealed class ConsistencyCheckResponse
{
    /// <summary>UTC.</summary>
    public DateTime LastRunAt { get; set; }

    /// <summary>Files missing on disk, damaged files, CSV files without an entry (details in the log).</summary>
    public int Problems { get; set; }
}

/// <summary>Jobs waiting or running per priority; running and failed in total.</summary>
public sealed class QueueResponse
{
    public int Recent { get; set; }

    public int Backfill { get; set; }

    public int RetryMissing { get; set; }

    public int Running { get; set; }

    public int Failed { get; set; }
}

public sealed class SourceStatusResponse
{
    public string Source { get; set; } = string.Empty;

    /// <summary>ok, rateLimited, unavailable</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>UTC</summary>
    public DateTime? LastSuccessAt { get; set; }

    public string? LastError { get; set; }

    /// <summary>UTC</summary>
    public DateTime? LastErrorAt { get; set; }

    /// <summary>UTC; end of a rate-limit pause.</summary>
    public DateTime? RetryAt { get; set; }
}

public sealed class CycleResponse
{
    /// <summary>UTC</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>UTC; null while the cycle runs.</summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>running, ok, partial, failed, interrupted, skipped</summary>
    public string Result { get; set; } = string.Empty;

    public int JobsPlanned { get; set; }

    public string? Message { get; set; }
}
