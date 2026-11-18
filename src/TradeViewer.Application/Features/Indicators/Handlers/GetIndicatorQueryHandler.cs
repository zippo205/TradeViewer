using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Indicators.Queries;
using TradeViewer.Application.Features.Indicators.Validators;
using TradeViewer.Contracts.Indicators;
using TradeViewer.Domain.Indicators;

namespace TradeViewer.Application.Features.Indicators.Handlers;

/// <summary>
/// Calculates an indicator for a page of candles (IN1): the page is read as the chart reads it, current candle
/// included, with the warm-up candles before it, so the values equal a calculation over the whole history and
/// two pages meet without a step.
/// </summary>
public sealed class GetIndicatorQueryHandler : IQueryHandler<GetIndicatorQuery, IndicatorResponse>
{
    private readonly GetIndicatorQueryValidator _validator = new GetIndicatorQueryValidator();
    private readonly ICandleSeriesReader _reader;

    public GetIndicatorQueryHandler(ICandleSeriesReader reader)
    {
        _reader = reader;
    }

    public async Task<Result<IndicatorResponse>> HandleAsync(GetIndicatorQuery query, CancellationToken cancellationToken)
    {
        var invalid = _validator.Validate(query, out var parameters);
        if (invalid != null)
        {
            return invalid;
        }

        var read = await _reader.ReadAsync(
            query.Source, query.Symbol, query.Timeframe, query.To, query.Limit, WarmUp.Candles(parameters!), cancellationToken);
        if (!read.IsSuccess)
        {
            return read.Error;
        }

        var page = read.Value;
        var lines = Indicator.Calculate(page.Candles, parameters!);

        var times = page.Page.Select(candle => candle.Time).ToArray();
        var values = new Dictionary<string, double?[]>();
        foreach (var line in lines)
        {
            values[line.Name] = line.Values[page.WarmUp..];
        }

        return Result.Success(new IndicatorResponse(times, values, page.Partial));
    }
}
