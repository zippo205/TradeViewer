using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Workspace.Queries;
using TradeViewer.Application.Features.Workspace.Services;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Handlers;

public sealed class GetSettingsQueryHandler : IQueryHandler<GetSettingsQuery, SettingsResponse>
{
    private readonly IHistorySettingsRepository _history;

    public GetSettingsQueryHandler(IHistorySettingsRepository history)
    {
        _history = history;
    }

    public async Task<Result<SettingsResponse>> HandleAsync(GetSettingsQuery query, CancellationToken cancellationToken)
    {
        return Result.Success(SettingsMapping.ToResponse(await _history.GetAsync(cancellationToken)));
    }
}
