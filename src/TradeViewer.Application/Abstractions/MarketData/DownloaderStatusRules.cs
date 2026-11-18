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

    /// <summary>The reason of a gap the source confirmed: it has no candles there and never will (MD4).</summary>
    public const string MissingUpstream = "missingUpstream";

    /// <summary>
    /// The gaps of a timeframe that are gaps of the asset (MD4): those the source confirmed, within the asset's history
    /// (HistoryStart in Unix seconds, null for the complete history), oldest first. The Downloader reports gaps for
    /// everything it holds, also for history another client wanted.
    /// </summary>
    public static IReadOnlyList<CoverageGap> ConfirmedGaps(TimeframeStatus? status, long? historyStart)
    {
        if (status == null)
        {
            return Array.Empty<CoverageGap>();
        }

        return status.Gaps
            .Where(gap => gap.Reason == MissingUpstream && gap.To > gap.From)
            .Where(gap => historyStart == null || gap.To.ToUnixTimeSeconds() > historyStart.Value)
            .OrderBy(gap => gap.From)
            .ToList();
    }

    public const string Green = "green";
    public const string Yellow = "yellow";
    public const string Red = "red";

    /// <summary>Switched off in the configuration, or not asked yet: nothing is known.</summary>
    public const string Off = "off";

    /// <summary>
    /// The colour of the status dot (DL1): red when the Downloader does not answer; yellow when it answers but a source
    /// is not ok (rate limited, unavailable), the last cycle did not finish well (partial, failed, interrupted), or
    /// its answer could not be read; green otherwise. Off when it is switched off or was not asked yet.
    /// </summary>
    public static string LightOf(bool enabled, bool? reachable, DownloaderServiceStatus? service)
    {
        if (!enabled || reachable == null)
        {
            return Off;
        }

        if (reachable == false)
        {
            return Red;
        }

        if (service == null)
        {
            return Yellow;
        }

        var sourceNotOk = service.Sources.Any(source => source.State != "ok");
        var cycleNotOk = service.LastCycle?.Result is "partial" or "failed" or "interrupted";
        return sourceNotOk || cycleNotOk ? Yellow : Green;
    }
}
