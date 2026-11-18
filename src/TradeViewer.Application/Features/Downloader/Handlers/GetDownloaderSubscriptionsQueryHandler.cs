using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Downloader.Queries;
using TradeViewer.Application.Features.Downloader.Services;
using TradeViewer.Contracts.Downloader;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.Downloader.Handlers;

public sealed class GetDownloaderSubscriptionsQueryHandler : IQueryHandler<GetDownloaderSubscriptionsQuery, DownloaderSubscriptionsResponse>
{
    private readonly IDownloaderClient _downloader;
    private readonly IDownloaderReachability _reachability;
    private readonly IAssetRepository _assets;

    public GetDownloaderSubscriptionsQueryHandler(IDownloaderClient downloader, IDownloaderReachability reachability, IAssetRepository assets)
    {
        _downloader = downloader;
        _reachability = reachability;
        _assets = assets;
    }

    public async Task<Result<DownloaderSubscriptionsResponse>> HandleAsync(GetDownloaderSubscriptionsQuery query, CancellationToken cancellationToken)
    {
        if (!_downloader.Enabled)
        {
            return Error.Unavailable("downloader.disabled", "The Downloader is switched off in the configuration (MarketData:Downloader:Enabled).");
        }

        var subscriptions = await _downloader.GetSubscriptionsAsync(cancellationToken);
        if (subscriptions.IsFailure)
        {
            await _reachability.ReportAsync(subscriptions.Error.Type != ErrorType.Unavailable, subscriptions.Error.Message);
            return subscriptions.Error;
        }

        await _reachability.ReportAsync(true, null);
        var assets = (await _assets.GetAllAsync(cancellationToken)).Select(asset => (asset.Source, asset.Symbol)).ToHashSet();
        var client = _downloader.Client;

        var items = subscriptions.Value
            .OrderBy(subscription => subscription.Source, StringComparer.Ordinal)
            .ThenBy(subscription => subscription.Symbol, StringComparer.Ordinal)
            .Select(subscription => new DownloaderSubscriptionResponse(
                subscription.Source,
                subscription.Symbol,
                subscription.SourceSymbol,
                subscription.SubscribedBy,
                subscription.SubscribedBy.Contains(client, StringComparer.Ordinal),
                assets.Contains((subscription.Source, subscription.Symbol)),
                subscription.Timeframes
                    .Select(timeframe => new DownloaderSubscribedTimeframeResponse(Timeframes.Code(timeframe.Timeframe), DownloaderMapping.Date(timeframe.WantedFrom), timeframe.SubscribedBy))
                    .ToList()))
            .ToList();

        return Result.Success(new DownloaderSubscriptionsResponse(client, items));
    }
}
