namespace TradeViewer.Contracts.Downloader;

/// <summary>
/// What the Downloader holds of an asset of the workspace (DL2). Live: asked just now; false: the last status the sync
/// read, of ReadAt, because the Downloader did not answer (null ReadAt: none is known). ImportErrors: files of the
/// asset that broke the format rules and were not imported (MD6).
/// </summary>
public sealed record DownloaderAssetResponse(
    string Source,
    string Symbol,
    bool Live,
    DateTimeOffset? ReadAt,
    string? Problem,
    long? LastChange,
    IReadOnlyList<DownloaderTimeframeResponse> Timeframes,
    IReadOnlyList<DownloaderImportErrorResponse> ImportErrors);

/// <summary>
/// Coverage of one timeframe: WantedFrom (yyyy-MM-dd, null complete), CoveredFrom and CoveredTo (exclusive), pending
/// jobs, gaps with reason (missingUpstream, unavailable, pending), when it last changed, the last error.
/// </summary>
public sealed record DownloaderTimeframeResponse(
    string Timeframe,
    string? WantedFrom,
    DateTimeOffset? CoveredFrom,
    DateTimeOffset? CoveredTo,
    int PendingJobs,
    IReadOnlyList<DownloaderGapResponse> Gaps,
    DateTimeOffset? LastChangedAt,
    string? LastError);

/// <summary>To is exclusive.</summary>
public sealed record DownloaderGapResponse(DateTimeOffset From, DateTimeOffset To, string Reason);

public sealed record DownloaderImportErrorResponse(string File, string Error);
