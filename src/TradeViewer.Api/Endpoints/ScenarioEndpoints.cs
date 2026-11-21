using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Scenarios.Commands;
using TradeViewer.Application.Features.Scenarios.Queries;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Api.Endpoints;

/// <summary>The scenario editor (SC1-SC16, SC20, SC21; architecture, chapter 9).</summary>
public static class ScenarioEndpoints
{
    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/scenarios", async (
            IQueryHandler<GetScenariosStatusQuery, ScenariosStatusResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetScenariosStatusQuery(), cancellationToken);
            return result.ToHttpResult();
        });

        var expert = routes.MapGroup("/api/scenarios/expert");

        expert.MapGet("", async (
            IQueryHandler<ListExpertScenariosQuery, IReadOnlyList<ExpertScenarioSummary>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ListExpertScenariosQuery(), cancellationToken);
            return result.ToHttpResult();
        });

        expert.MapPost("", async (
            ExpertScenarioRecord scenario,
            ICommandHandler<SaveExpertScenarioCommand, ExpertScenarioResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new SaveExpertScenarioCommand(null, scenario), cancellationToken);
            return result.ToHttpResult();
        });

        // timeframe (optional): the timeframe of the chart; the points of higher levels are drawn on its candles (decision 0013).
        expert.MapPost("/snap", async (
            SnapScenarioPointRequest request,
            string? timeframe,
            IQueryHandler<SnapScenarioPointQuery, ExpertScenarioResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new SnapScenarioPointQuery(request, timeframe), cancellationToken);
            return result.ToHttpResult();
        });

        expert.MapPost("/preview", async (
            ExpertScenarioRecord scenario,
            string? timeframe,
            IQueryHandler<PreviewExpertScenarioQuery, ExpertScenarioResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new PreviewExpertScenarioQuery(scenario, timeframe), cancellationToken);
            return result.ToHttpResult();
        });

        expert.MapGet("/{id}", async (
            string id,
            string? timeframe,
            IQueryHandler<GetExpertScenarioQuery, ExpertScenarioResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetExpertScenarioQuery(id, timeframe), cancellationToken);
            return result.ToHttpResult();
        });

        expert.MapPut("/{id}", async (
            string id,
            ExpertScenarioRecord scenario,
            ICommandHandler<SaveExpertScenarioCommand, ExpertScenarioResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new SaveExpertScenarioCommand(id, scenario), cancellationToken);
            return result.ToHttpResult();
        });

        expert.MapDelete("/{id}", async (
            string id,
            ICommandHandler<DeleteExpertScenarioCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new DeleteExpertScenarioCommand(id), cancellationToken);
            return result.ToNoContentResult();
        });

        return routes;
    }
}
