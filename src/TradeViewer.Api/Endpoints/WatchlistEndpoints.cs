using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Watchlists.Commands;
using TradeViewer.Application.Features.Watchlists.Queries;
using TradeViewer.Contracts.Watchlists;

namespace TradeViewer.Api.Endpoints;

public static class WatchlistEndpoints
{
    public static IEndpointRouteBuilder MapWatchlistEndpoints(this IEndpointRouteBuilder routes)
    {
        var watchlists = routes.MapGroup("/api/watchlists");

        watchlists.MapGet("", async (
            IQueryHandler<GetWatchlistsQuery, IReadOnlyList<WatchlistResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetWatchlistsQuery(), cancellationToken);
            return result.ToHttpResult();
        });

        watchlists.MapPut("/{assetClass}/order", async (
            string assetClass,
            ReorderWatchlistRequest request,
            ICommandHandler<ReorderWatchlistCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ReorderWatchlistCommand(assetClass, request.Assets), cancellationToken);
            return result.ToNoContentResult();
        });

        return routes;
    }
}
