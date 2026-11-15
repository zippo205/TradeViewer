using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Assets.Commands;
using TradeViewer.Application.Features.Assets.Queries;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Api.Endpoints;

public static class AssetEndpoints
{
    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/symbols/{source}", async (
            string source,
            string? q,
            IQueryHandler<GetSymbolsQuery, IReadOnlyList<SymbolResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetSymbolsQuery(source, q), cancellationToken);
            return result.ToHttpResult();
        });

        var assets = routes.MapGroup("/api/assets");

        assets.MapGet("", async (
            IQueryHandler<GetAssetsQuery, IReadOnlyList<AssetResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetAssetsQuery(), cancellationToken);
            return result.ToHttpResult();
        });

        assets.MapPost("", async (
            AddAssetRequest request,
            ICommandHandler<AddAssetCommand, AssetResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new AddAssetCommand(request.Source, request.Symbol), cancellationToken);
            return result.ToHttpResult();
        });

        assets.MapGet("/{source}/{symbol}", async (
            string source,
            string symbol,
            IQueryHandler<GetAssetQuery, AssetResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetAssetQuery(source, symbol), cancellationToken);
            return result.ToHttpResult();
        });

        assets.MapPut("/{source}/{symbol}/history", async (
            string source,
            string symbol,
            ChangeHistoryRequest request,
            ICommandHandler<ChangeAssetHistoryCommand, AssetResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ChangeAssetHistoryCommand(source, symbol, request.HistoryFrom), cancellationToken);
            return result.ToHttpResult();
        });

        assets.MapDelete("/{source}/{symbol}", async (
            string source,
            string symbol,
            ICommandHandler<RemoveAssetCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new RemoveAssetCommand(source, symbol), cancellationToken);
            return result.ToNoContentResult();
        });

        assets.MapPost("/{source}/{symbol}/rescan", async (
            string source,
            string symbol,
            ICommandHandler<RescanAssetCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new RescanAssetCommand(source, symbol), cancellationToken);
            return result.ToNoContentResult();
        });

        assets.MapPost("/{source}/{symbol}/rebuild", async (
            string source,
            string symbol,
            ICommandHandler<RebuildAssetCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new RebuildAssetCommand(source, symbol), cancellationToken);
            return result.ToNoContentResult();
        });

        return routes;
    }
}
