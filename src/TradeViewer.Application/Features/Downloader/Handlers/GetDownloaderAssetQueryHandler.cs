using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Downloader.Queries;
using TradeViewer.Application.Features.Downloader.Services;
using TradeViewer.Contracts.Downloader;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.Downloader.Handlers;

/// <summary>
/// Coverage of an asset (DL2), asked now; when the Downloader does not answer or is switched off, the last status the
/// sync read, marked with its time, so the page still shows what was known (DL4).
/// </summary>
public sealed class GetDownloaderAssetQueryHandler : IQueryHandler<GetDownloaderAssetQuery, DownloaderAssetResponse>
{
    private readonly IAssetRepository _assets;
    private readonly IDownloaderClient _downloader;
    private readonly IDownloaderReachability _reachability;
    private readonly ICandleStore _store;

    public GetDownloaderAssetQueryHandler(IAssetRepository assets, IDownloaderClient downloader, IDownloaderReachability reachability, ICandleStore store)
    {
        _assets = assets;
        _downloader = downloader;
        _reachability = reachability;
        _store = store;
    }

    public async Task<Result<DownloaderAssetResponse>> HandleAsync(GetDownloaderAssetQuery query, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(query.Source, query.Symbol, cancellationToken);
        if (asset == null)
        {
            return Error.NotFound("asset.notFound", "There is no asset " + query.Source + "/" + query.Symbol + ".");
        }

        var errors = _store.GetImportErrors(asset.Source, asset.Symbol)
            .Select(entry => new DownloaderImportErrorResponse(entry.File, entry.Error ?? string.Empty))
            .ToList();

        string problem;
        if (_downloader.Enabled)
        {
            var status = await _downloader.GetSymbolStatusAsync(asset.Source, asset.Symbol, cancellationToken);
            if (status.IsSuccess)
            {
                await _reachability.ReportAsync(true, null);
                return Result.Success(Response(asset.Source, asset.Symbol, true, null, null, status.Value, errors));
            }

            if (status.Error.Type == ErrorType.NotFound)
            {
                // It answers but has no subscription for the symbol: the sync subscribes it again.
                await _reachability.ReportAsync(true, null);
                return Result.Success(Response(asset.Source, asset.Symbol, true, null, status.Error.Message, null, errors));
            }

            await _reachability.ReportAsync(status.Error.Type != ErrorType.Unavailable, status.Error.Message);
            problem = status.Error.Message;
        }
        else
        {
            problem = "The Downloader is switched off in the configuration (MarketData:Downloader:Enabled).";
        }

        var known = _store.GetSyncState(asset.Source, asset.Symbol);
        var readAt = known?.LastStatus == null ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeSeconds(known.CheckedAt);
        return Result.Success(Response(asset.Source, asset.Symbol, false, readAt, problem, known?.LastStatus, errors));
    }

    private static DownloaderAssetResponse Response(
        string source, string symbol, bool live, DateTimeOffset? readAt, string? problem, SymbolStatus? status, IReadOnlyList<DownloaderImportErrorResponse> errors)
    {
        var timeframes = status?.Timeframes
            .OrderBy(timeframe => Timeframes.Seconds(timeframe.Timeframe))
            .Select(DownloaderMapping.ToResponse)
            .ToList() ?? new List<DownloaderTimeframeResponse>();
        return new DownloaderAssetResponse(source, symbol, live, readAt, problem, status?.LastChange, timeframes, errors);
    }
}
