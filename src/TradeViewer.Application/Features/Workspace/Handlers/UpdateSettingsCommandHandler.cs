using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Workspace.Commands;
using TradeViewer.Application.Features.Workspace.Services;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Handlers;

public sealed class UpdateSettingsCommandHandler : ICommandHandler<UpdateSettingsCommand, SettingsResponse>
{
    private readonly IHistorySettingsRepository _history;

    public UpdateSettingsCommandHandler(IHistorySettingsRepository history)
    {
        _history = history;
    }

    public async Task<Result<SettingsResponse>> HandleAsync(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        var history = SettingsMapping.ToHistory(command.DefaultHistory);
        if (history.IsFailure)
        {
            return history.Error;
        }

        await _history.SetAsync(history.Value, cancellationToken);
        return Result.Success(SettingsMapping.ToResponse(history.Value));
    }
}
