using System.Globalization;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Contracts.Downloader;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.Downloader.Services;

/// <summary>The models of the Downloader client as the monitoring page gets them.</summary>
public static class DownloaderMapping
{
    public static DownloaderServiceResponse ToResponse(DownloaderServiceStatus status)
    {
        return new DownloaderServiceResponse(
            status.State,
            status.Version,
            status.StartedAt,
            status.LastCycle == null
                ? null
                : new DownloaderCycleResponse(status.LastCycle.StartedAt, status.LastCycle.FinishedAt, status.LastCycle.Result, status.LastCycle.JobsPlanned, status.LastCycle.Message),
            status.NextCycleAt,
            new DownloaderQueueResponse(status.Queue.Recent, status.Queue.Backfill, status.Queue.RetryMissing, status.Queue.Running, status.Queue.Failed),
            status.Sources.Select(source => new DownloaderSourceResponse(source.Source, source.State, source.LastSuccessAt, source.LastError, source.LastErrorAt, source.RetryAt)).ToList(),
            status.Catalogs.Select(catalog => new DownloaderCatalogResponse(catalog.Source, catalog.RefreshedAt, catalog.SymbolCount, catalog.LastError, catalog.LastErrorAt)).ToList(),
            status.ConsistencyCheck == null ? null : new DownloaderCheckResponse(status.ConsistencyCheck.LastRunAt, status.ConsistencyCheck.Problems));
    }

    public static DownloaderTimeframeResponse ToResponse(TimeframeStatus status)
    {
        return new DownloaderTimeframeResponse(
            Timeframes.Code(status.Timeframe),
            Date(status.WantedFrom),
            status.CoveredFrom,
            status.CoveredTo,
            status.PendingJobs,
            status.Gaps.Select(gap => new DownloaderGapResponse(gap.From, gap.To, gap.Reason)).ToList(),
            status.LastChangedAt,
            status.LastError);
    }

    public static string? Date(DateOnly? date)
    {
        return date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
