using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Scenarios.Queries;
using TradeViewer.Application.Features.Scenarios.Services;
using TradeViewer.Application.Features.Scenarios.Validators;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Handlers;

public sealed class PreviewExpertScenarioQueryHandler : IQueryHandler<PreviewExpertScenarioQuery, ExpertScenarioResponse>
{
    private readonly ExpertScenarioResponses _responses;

    public PreviewExpertScenarioQueryHandler(ExpertScenarioResponses responses)
    {
        _responses = responses;
    }

    public async Task<Result<ExpertScenarioResponse>> HandleAsync(PreviewExpertScenarioQuery query, CancellationToken cancellationToken)
    {
        var invalid = ExpertScenarioValidator.Validate(query.Scenario);
        if (invalid != null)
        {
            return invalid;
        }

        return Result.Success(await _responses.BuildAsync(query.Scenario, cancellationToken));
    }
}
