using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Downloader.Queries;
using TradeViewer.Application.Features.Downloader.Services;
using TradeViewer.Contracts.Downloader;

namespace TradeViewer.Application.Features.Downloader.Handlers;

/// <summary>
/// Asks the Downloader for its state and reports the answer or the failure (DL4), so the dot of every window learns of
/// a change also when no asset is polled. The page only shows data: nothing at the Downloader changes (DL1).
/// </summary>
public sealed class GetDownloaderStatusQueryHandler : IQueryHandler<GetDownloaderStatusQuery, DownloaderStatusResponse>
{
    private readonly IDownloaderClient _downloader;
    private readonly IDownloaderReachability _reachability;

    public GetDownloaderStatusQueryHandler(IDownloaderClient downloader, IDownloaderReachability reachability)
    {
        _downloader = downloader;
        _reachability = reachability;
    }

    public async Task<Result<DownloaderStatusResponse>> HandleAsync(GetDownloaderStatusQuery query, CancellationToken cancellationToken)
    {
        if (!_downloader.Enabled)
        {
            return Result.Success(new DownloaderStatusResponse(
                false, null, null, null, "The Downloader is switched off in the configuration (MarketData:Downloader:Enabled).", DownloaderStatusRules.Off, null));
        }

        var status = await _downloader.GetServiceStatusAsync(cancellationToken);
        string? problem = null;
        if (status.IsSuccess)
        {
            await _reachability.ReportAsync(true, null);
        }
        else
        {
            problem = status.Error.Message;

            // Any answer, also one that could not be read, shows the Downloader is there.
            await _reachability.ReportAsync(status.Error.Type != ErrorType.Unavailable, status.Error.Message);
        }

        var service = status.IsSuccess ? status.Value : null;
        var state = _reachability.Current;
        return Result.Success(new DownloaderStatusResponse(
            true,
            state.Reachable,
            state.UnreachableSince,
            state.CheckedAt,
            problem,
            DownloaderStatusRules.LightOf(true, state.Reachable, service),
            service == null ? null : DownloaderMapping.ToResponse(service)));
    }
}
