using TradeViewer.Contracts.Health;

namespace TradeViewer.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", () => Results.Ok(new HealthResponse("ok")));
        return routes;
    }
}
