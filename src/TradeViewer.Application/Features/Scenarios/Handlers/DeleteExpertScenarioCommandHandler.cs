using TradeViewer.Application.Abstractions.Ewts;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Scenarios.Commands;
using TradeViewer.Application.Features.Scenarios.Services;

namespace TradeViewer.Application.Features.Scenarios.Handlers;

public sealed class DeleteExpertScenarioCommandHandler : ICommandHandler<DeleteExpertScenarioCommand, Unit>
{
    private readonly IExpertScenarioStore _store;

    public DeleteExpertScenarioCommandHandler(IExpertScenarioStore store)
    {
        _store = store;
    }

    public async Task<Result<Unit>> HandleAsync(DeleteExpertScenarioCommand command, CancellationToken cancellationToken)
    {
        if (!_store.Enabled)
        {
            return ScenarioErrors.Off();
        }

        if (!_store.Writable)
        {
            return ScenarioErrors.ReadOnly();
        }

        return await _store.DeleteAsync(command.Id, cancellationToken) ? Result.Success() : ScenarioErrors.NotFound(command.Id);
    }
}
