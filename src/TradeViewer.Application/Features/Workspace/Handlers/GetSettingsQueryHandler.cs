using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Workspace.Queries;
using TradeViewer.Application.Features.Workspace.Services;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Handlers;

public sealed class GetSettingsQueryHandler : IQueryHandler<GetSettingsQuery, SettingsResponse>
{
    private readonly IHistorySettingsRepository _history;
    private readonly ISettingsRepository _settings;

    public GetSettingsQueryHandler(IHistorySettingsRepository history, ISettingsRepository settings)
    {
        _history = history;
        _settings = settings;
    }

    public async Task<Result<SettingsResponse>> HandleAsync(GetSettingsQuery query, CancellationToken cancellationToken)
    {
        var history = await _history.GetAsync(cancellationToken);
        var lastChart = await LastChartSetting.ReadAsync(_settings, cancellationToken);
        return Result.Success(SettingsMapping.ToResponse(history, lastChart));
    }
}
