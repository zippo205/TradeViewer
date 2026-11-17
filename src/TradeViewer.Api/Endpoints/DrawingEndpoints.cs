using Microsoft.AspNetCore.Mvc;
using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Drawings.Commands;
using TradeViewer.Application.Features.Drawings.Queries;
using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Api.Endpoints;

public static class DrawingEndpoints
{
    /// <summary>The header naming the window that makes a change; it ignores its own DrawingsChanged event.</summary>
    public const string WindowHeader = "X-TradeViewer-Window";

    public static IEndpointRouteBuilder MapDrawingEndpoints(this IEndpointRouteBuilder routes)
    {
        var drawings = routes.MapGroup("/api/drawings");

        drawings.MapGet("/{source}/{symbol}", async (
            string source,
            string symbol,
            IQueryHandler<GetDrawingsQuery, IReadOnlyList<DrawingRecord>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetDrawingsQuery(source, symbol), cancellationToken);
            return result.ToHttpResult();
        });

        drawings.MapPut("/{id:guid}", async (
            Guid id,
            DrawingRecord drawing,
            [FromHeader(Name = WindowHeader)] string? window,
            ICommandHandler<SaveDrawingCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new SaveDrawingCommand(id, drawing, window), cancellationToken);
            return result.ToNoContentResult();
        });

        drawings.MapDelete("/{id:guid}", async (
            Guid id,
            [FromHeader(Name = WindowHeader)] string? window,
            ICommandHandler<DeleteDrawingCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new DeleteDrawingCommand(id, window), cancellationToken);
            return result.ToNoContentResult();
        });

        var presets = routes.MapGroup("/api/fibonacci-presets");

        presets.MapGet("", async (
            IQueryHandler<GetFibonacciPresetsQuery, IReadOnlyList<FibonacciPresetRecord>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetFibonacciPresetsQuery(), cancellationToken);
            return result.ToHttpResult();
        });

        presets.MapPut("/{id:guid}", async (
            Guid id,
            FibonacciPresetRecord preset,
            ICommandHandler<SaveFibonacciPresetCommand, FibonacciPresetRecord> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new SaveFibonacciPresetCommand(id, preset), cancellationToken);
            return result.ToHttpResult();
        });

        presets.MapDelete("/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteFibonacciPresetCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new DeleteFibonacciPresetCommand(id), cancellationToken);
            return result.ToNoContentResult();
        });

        return routes;
    }
}
