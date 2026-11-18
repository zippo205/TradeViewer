namespace TradeViewer.Contracts.Downloader;

/// <summary>
/// The Downloader as TradeViewer sees it now (DL1, DL4). Enabled false: switched off in the configuration, nothing is
/// asked. Reachable null: not asked yet. UnreachableSince: the first failure of the current run of failures.
/// Problem: why the last question failed. Light: green, yellow (a source is not ok, or the last cycle was partial or
/// failed), red (not reachable) or off. Service: what the Downloader answered, null when it did not.
/// </summary>
public sealed record DownloaderStatusResponse(
    bool Enabled,
    bool? Reachable,
    DateTimeOffset? UnreachableSince,
    DateTimeOffset? CheckedAt,
    string? Problem,
    string Light,
    DownloaderServiceResponse? Service);

/// <summary>GET status of the Downloader. LastCycle null before the first; NextCycleAt null when cycles are off.</summary>
public sealed record DownloaderServiceResponse(
    string State,
    string Version,
    DateTimeOffset StartedAt,
    DownloaderCycleResponse? LastCycle,
    DateTimeOffset? NextCycleAt,
    DownloaderQueueResponse Queue,
    IReadOnlyList<DownloaderSourceResponse> Sources,
    IReadOnlyList<DownloaderCatalogResponse> Catalogs,
    DownloaderCheckResponse? ConsistencyCheck);

/// <summary>Result: running, ok, partial, failed, interrupted, skipped.</summary>
public sealed record DownloaderCycleResponse(DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Result, int JobsPlanned, string? Message);

public sealed record DownloaderQueueResponse(int Recent, int Backfill, int RetryMissing, int Running, int Failed);

/// <summary>State: ok, rateLimited (until RetryAt), unavailable.</summary>
public sealed record DownloaderSourceResponse(string Source, string State, DateTimeOffset? LastSuccessAt, string? LastError, DateTimeOffset? LastErrorAt, DateTimeOffset? RetryAt);

public sealed record DownloaderCatalogResponse(string Source, DateTimeOffset? RefreshedAt, int SymbolCount, string? LastError, DateTimeOffset? LastErrorAt);

/// <summary>The weekly check of the Downloader's files: missing, damaged or unknown files found.</summary>
public sealed record DownloaderCheckResponse(DateTimeOffset LastRunAt, int Problems);
