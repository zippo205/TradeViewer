namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>What the status of the Downloader means for TradeViewer; one place for the rules several features use.</summary>
public static class DownloaderStatusRules
{
    /// <summary>
    /// The Downloader still downloads a timeframe while it has jobs for it or reports a part as "pending".
    /// What the source does not have (before its history limit, an outage) is nothing to wait for (AS4).
    /// </summary>
    public static bool IsDownloading(TimeframeStatus? status)
    {
        return status != null && (status.PendingJobs > 0 || status.Gaps.Any(gap => gap.Reason == "pending"));
    }

    public static bool IsDownloading(SymbolStatus? status)
    {
        return status != null && status.Timeframes.Any(timeframe => IsDownloading(timeframe));
    }

    /// <summary>The gaps the source confirmed as missing, over all timeframes (MD4).</summary>
    public static int MissingUpstreamGaps(SymbolStatus? status)
    {
        return status == null ? 0 : status.Timeframes.Sum(timeframe => timeframe.Gaps.Count(gap => gap.Reason == "missingUpstream"));
    }
}
