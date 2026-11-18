using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Downloader.Queries;
using TradeViewer.Contracts.Downloader;

namespace TradeViewer.Api.Endpoints;

public static class DownloaderEndpoints
{
    /// <summary>The monitoring page (DL1-DL4): it only reads; nothing at the Downloader changes.</summary>
    public static IEndpointRouteBuilder MapDownloaderEndpoints(this IEndpointRouteBuilder routes)
    {
        var downloader = routes.MapGroup("/api/downloader");

        downloader.MapGet("/status", async (IQueryHandler<GetDownloaderStatusQuery, DownloaderStatusResponse> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(new GetDownloaderStatusQuery(), cancellationToken)).ToHttpResult());

        downloader.MapGet("/subscriptions", async (IQueryHandler<GetDownloaderSubscriptionsQuery, DownloaderSubscriptionsResponse> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(new GetDownloaderSubscriptionsQuery(), cancellationToken)).ToHttpResult());

        downloader.MapGet("/assets/{source}/{symbol}", async (
            string source,
            string symbol,
            IQueryHandler<GetDownloaderAssetQuery, DownloaderAssetResponse> handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(new GetDownloaderAssetQuery(source, symbol), cancellationToken)).ToHttpResult());

        return routes;
    }
}
