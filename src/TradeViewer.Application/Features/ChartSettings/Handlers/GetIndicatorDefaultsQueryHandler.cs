using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.ChartSettings.Queries;
using TradeViewer.Application.Features.ChartSettings.Services;
using TradeViewer.Contracts.Indicators;
using TradeViewer.Domain.Indicators;

namespace TradeViewer.Application.Features.ChartSettings.Handlers;

public sealed class GetIndicatorDefaultsQueryHandler : IQueryHandler<GetIndicatorDefaultsQuery, IReadOnlyList<IndicatorRecord>>
{
    public Task<Result<IReadOnlyList<IndicatorRecord>>> HandleAsync(GetIndicatorDefaultsQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<IndicatorRecord> defaults = Enum.GetValues<IndicatorType>()
            .Select(type => IndicatorMapping.ToRecord(IndicatorInstance.New(Guid.Empty, type)))
            .ToList();
        return Task.FromResult(Result.Success(defaults));
    }
}
