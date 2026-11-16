using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.MarketData.Queries;
using TradeViewer.Contracts.MarketData;

namespace TradeViewer.Api.Endpoints;

public static class CandleEndpoints
{
    public static IEndpointRouteBuilder MapCandleEndpoints(this IEndpointRouteBuilder routes)
    {
        // The chart module calls this endpoint directly; scrolling back asks for the page before the oldest candle it has (CH4).
        routes.MapGet("/api/candles/{source}/{symbol}/{timeframe}", async (
            string source,
            string symbol,
            string timeframe,
            long? to,
            int? limit,
            IQueryHandler<GetCandlesQuery, CandlesResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetCandlesQuery(source, symbol, timeframe, to, limit), cancellationToken);
            return result.ToHttpResult();
        });

        // The gaps the source confirmed, for the markers of the chart (MD4).
        routes.MapGet("/api/gaps/{source}/{symbol}/{timeframe}", async (
            string source,
            string symbol,
            string timeframe,
            IQueryHandler<GetGapsQuery, IReadOnlyList<GapResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetGapsQuery(source, symbol, timeframe), cancellationToken);
            return result.ToHttpResult();
        });

        return routes;
    }
}
