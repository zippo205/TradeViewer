using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Indicators.Queries;
using TradeViewer.Contracts.Indicators;

namespace TradeViewer.Api.Endpoints;

public static class IndicatorEndpoints
{
    public static IEndpointRouteBuilder MapIndicatorEndpoints(this IEndpointRouteBuilder routes)
    {
        // The chart module asks for each indicator with the to and limit of the candle page it shows (IN1).
        routes.MapGet("/api/indicators/{source}/{symbol}/{timeframe}", async (
            string source,
            string symbol,
            string timeframe,
            string? type,
            int? length,
            double? deviation,
            int? fast,
            int? slow,
            int? signal,
            string? price,
            long? to,
            int? limit,
            IQueryHandler<GetIndicatorQuery, IndicatorResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetIndicatorQuery(source, symbol, timeframe, type, length, deviation, fast, slow, signal, price, to, limit);
            var result = await handler.HandleAsync(query, cancellationToken);
            return result.ToHttpResult();
        });

        return routes;
    }
}
