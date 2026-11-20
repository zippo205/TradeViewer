using TradeViewer.Application.Abstractions.Ewts;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Scenarios.Queries;
using TradeViewer.Application.Features.Scenarios.Services;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Handlers;

public sealed class ListExpertScenariosQueryHandler : IQueryHandler<ListExpertScenariosQuery, IReadOnlyList<ExpertScenarioSummary>>
{
    private readonly IExpertScenarioStore _store;

    public ListExpertScenariosQueryHandler(IExpertScenarioStore store)
    {
        _store = store;
    }

    public async Task<Result<IReadOnlyList<ExpertScenarioSummary>>> HandleAsync(ListExpertScenariosQuery query, CancellationToken cancellationToken)
    {
        if (!_store.Enabled)
        {
            return ScenarioErrors.Off();
        }

        return Result.Success(await _store.ListAsync(cancellationToken));
    }
}
