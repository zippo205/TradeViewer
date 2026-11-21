using TradeViewer.Application.Abstractions.Ewts;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Scenarios.Queries;
using TradeViewer.Application.Features.Scenarios.Services;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Handlers;

/// <summary>Reads an expert scenario as it is written: opening never moves a point and writes nothing (SC13).</summary>
public sealed class GetExpertScenarioQueryHandler : IQueryHandler<GetExpertScenarioQuery, ExpertScenarioResponse>
{
    private readonly IExpertScenarioStore _store;
    private readonly ExpertScenarioResponses _responses;

    public GetExpertScenarioQueryHandler(IExpertScenarioStore store, ExpertScenarioResponses responses)
    {
        _store = store;
        _responses = responses;
    }

    public async Task<Result<ExpertScenarioResponse>> HandleAsync(GetExpertScenarioQuery query, CancellationToken cancellationToken)
    {
        if (!_store.Enabled)
        {
            return ScenarioErrors.Off();
        }

        var read = await _store.ReadAsync(query.Id, cancellationToken);
        if (read.Unreadable)
        {
            return ScenarioErrors.Unreadable(query.Id, read.Error!);
        }

        if (!read.Found)
        {
            return ScenarioErrors.NotFound(query.Id);
        }

        return Result.Success(await _responses.BuildAsync(read.Scenario!, query.Timeframe, cancellationToken));
    }
}
