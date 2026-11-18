namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>
/// Whether the Downloader answered the last time TradeViewer asked, and since when it does not (DL4). The sync and
/// the status page report every answer and failure; a change is logged once and sent to the open windows
/// (DownloaderStatusChanged). Kept in memory: after a start the first question finds it out again.
/// </summary>
public interface IDownloaderReachability
{
    DownloaderReachabilityState Current { get; }

    /// <summary>An answer (true) or a failure to reach the Downloader (false), with the reason of a failure.</summary>
    Task ReportAsync(bool reachable, string? reason);
}

/// <summary>
/// Reachable null: not asked yet. UnreachableSince: the first failure of the current run of failures, null while it
/// answers. CheckedAt: the last report. Reason: the message of the last failure.
/// </summary>
public sealed record DownloaderReachabilityState(bool? Reachable, DateTimeOffset? UnreachableSince, DateTimeOffset? CheckedAt, string? Reason)
{
    public static DownloaderReachabilityState Unknown { get; } = new DownloaderReachabilityState(null, null, null, null);
}
