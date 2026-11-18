using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.ChartSettings.Queries;
using TradeViewer.Application.Features.ChartSettings.Services;
using TradeViewer.Contracts.Indicators;

namespace TradeViewer.Application.Features.ChartSettings.Handlers;

public sealed class GetIndicatorTemplatesQueryHandler : IQueryHandler<GetIndicatorTemplatesQuery, IReadOnlyList<IndicatorTemplateRecord>>
{
    private readonly IIndicatorTemplateRepository _templates;

    public GetIndicatorTemplatesQueryHandler(IIndicatorTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task<Result<IReadOnlyList<IndicatorTemplateRecord>>> HandleAsync(GetIndicatorTemplatesQuery query, CancellationToken cancellationToken)
    {
        var templates = await _templates.GetAllAsync(cancellationToken);
        IReadOnlyList<IndicatorTemplateRecord> records = templates.Select(IndicatorMapping.ToRecord).ToList();
        return Result.Success(records);
    }
}
