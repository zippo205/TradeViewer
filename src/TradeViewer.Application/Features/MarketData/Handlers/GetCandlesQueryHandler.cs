using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.MarketData.Queries;
using TradeViewer.Contracts.MarketData;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.MarketData.Handlers;

public sealed class GetCandlesQueryHandler : IQueryHandler<GetCandlesQuery, CandlesResponse>
{
    private readonly ICandleSeriesReader _reader;

    public GetCandlesQueryHandler(ICandleSeriesReader reader)
    {
        _reader = reader;
    }

    public async Task<Result<CandlesResponse>> HandleAsync(GetCandlesQuery query, CancellationToken cancellationToken)
    {
        var page = await _reader.ReadAsync(query.Source, query.Symbol, query.Timeframe, query.To, query.Limit, 0, cancellationToken);
        if (!page.IsSuccess)
        {
            return page.Error;
        }

        return Result.Success(ToColumns(page.Value.Candles, page.Value.Partial));
    }

    private static CandlesResponse ToColumns(IReadOnlyList<Candle> candles, bool partial)
    {
        var count = candles.Count;
        var t = new long[count];
        var o = new double[count];
        var h = new double[count];
        var l = new double[count];
        var c = new double[count];
        var v = new double[count];

        for (var index = 0; index < count; index++)
        {
            var candle = candles[index];
            t[index] = candle.Time;
            o[index] = candle.Open;
            h[index] = candle.High;
            l[index] = candle.Low;
            c[index] = candle.Close;
            v[index] = candle.Volume;
        }

        return new CandlesResponse(t, o, h, l, c, v, partial);
    }
}
