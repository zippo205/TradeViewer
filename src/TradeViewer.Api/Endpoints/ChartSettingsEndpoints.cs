using Microsoft.AspNetCore.Mvc;
using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Application.Features.ChartSettings.Queries;
using TradeViewer.Contracts.Charts;
using TradeViewer.Contracts.Indicators;

namespace TradeViewer.Api.Endpoints;

public static class ChartSettingsEndpoints
{
    public static IEndpointRouteBuilder MapChartSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var settings = routes.MapGroup("/api/chart-settings");

        settings.MapGet("/{source}/{symbol}", async (
            string source,
            string symbol,
            IQueryHandler<GetChartSettingsQuery, ChartSettingsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetChartSettingsQuery(source, symbol), cancellationToken);
            return result.ToHttpResult();
        });

        settings.MapPut("/{source}/{symbol}", async (
            string source,
            string symbol,
            ChartSettingsResponse request,
            [FromHeader(Name = DrawingEndpoints.WindowHeader)] string? window,
            ICommandHandler<SaveChartSettingsCommand, ChartSettingsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new SaveChartSettingsCommand(source, symbol, request, window), cancellationToken);
            return result.ToHttpResult();
        });

        // Indicator templates (IN2): applying one is done by the client, which puts copies of its indicators into the chart settings.
        var templates = routes.MapGroup("/api/indicator-templates");

        templates.MapGet("", async (
            IQueryHandler<GetIndicatorTemplatesQuery, IReadOnlyList<IndicatorTemplateRecord>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetIndicatorTemplatesQuery(), cancellationToken);
            return result.ToHttpResult();
        });

        templates.MapPut("/{id:guid}", async (
            Guid id,
            IndicatorTemplateRecord template,
            ICommandHandler<SaveIndicatorTemplateCommand, IndicatorTemplateRecord> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new SaveIndicatorTemplateCommand(id, template), cancellationToken);
            return result.ToHttpResult();
        });

        templates.MapDelete("/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteIndicatorTemplateCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new DeleteIndicatorTemplateCommand(id), cancellationToken);
            return result.ToNoContentResult();
        });

        return routes;
    }
}
