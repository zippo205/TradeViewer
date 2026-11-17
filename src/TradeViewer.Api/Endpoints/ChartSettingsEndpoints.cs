using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Application.Features.ChartSettings.Queries;
using TradeViewer.Contracts.Charts;

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
            ICommandHandler<SaveChartSettingsCommand, ChartSettingsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new SaveChartSettingsCommand(source, symbol, request), cancellationToken);
            return result.ToHttpResult();
        });

        return routes;
    }
}
