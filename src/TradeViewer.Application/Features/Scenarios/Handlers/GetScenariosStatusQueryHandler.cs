using TradeViewer.Application.Abstractions.Ewts;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Scenarios.Queries;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Handlers;

public sealed class GetScenariosStatusQueryHandler : IQueryHandler<GetScenariosStatusQuery, ScenariosStatusResponse>
{
    private readonly IExpertScenarioStore _store;

    public GetScenariosStatusQueryHandler(IExpertScenarioStore store)
    {
        _store = store;
    }

    public Task<Result<ScenariosStatusResponse>> HandleAsync(GetScenariosStatusQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new ScenariosStatusResponse(_store.Enabled, _store.Writable)));
    }
}
