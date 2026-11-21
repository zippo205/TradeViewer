using TradeViewer.Application.Abstractions.Ewts;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Scenarios.Commands;
using TradeViewer.Application.Features.Scenarios.Services;
using TradeViewer.Application.Features.Scenarios.Validators;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Handlers;

/// <summary>
/// Writes an expert scenario (SC13): the first save takes the id (SC14), every later one keeps the file's unknown
/// fields. Form problems do not prevent it (SC15) except levels that do not fit (decision 0013); otherwise only what
/// could not be read again is refused. Last save wins (WS1).
/// </summary>
public sealed class SaveExpertScenarioCommandHandler : ICommandHandler<SaveExpertScenarioCommand, ExpertScenarioResponse>
{
    private readonly IExpertScenarioStore _store;
    private readonly ExpertScenarioResponses _responses;
    private readonly ScenarioTurnings _turnings;

    public SaveExpertScenarioCommandHandler(IExpertScenarioStore store, ExpertScenarioResponses responses, ScenarioTurnings turnings)
    {
        _store = store;
        _responses = responses;
        _turnings = turnings;
    }

    public async Task<Result<ExpertScenarioResponse>> HandleAsync(SaveExpertScenarioCommand command, CancellationToken cancellationToken)
    {
        if (!_store.Enabled)
        {
            return ScenarioErrors.Off();
        }

        if (!_store.Writable)
        {
            return ScenarioErrors.ReadOnly();
        }

        var invalid = ExpertScenarioValidator.Validate(command.Scenario);
        if (invalid != null)
        {
            return invalid;
        }

        if (command.Id == null && command.Scenario.Id != null)
        {
            return ScenarioErrors.Field("id", "A new scenario has no id; the first save takes it.");
        }

        if (command.Id != null && command.Scenario.Id != command.Id)
        {
            return ScenarioErrors.Field("id", "The id of the scenario must be the id in the address.");
        }

        // Decision 0013: levels that do not fit are not saved; the editor shows the misfit and keeps Save disabled.
        var misfit = (await _turnings.MisfitsAsync(command.Scenario, cancellationToken)).FirstOrDefault();
        if (misfit != null)
        {
            return Error.InvalidData("scenario.levelsDoNotFit", misfit.Field + ": " + misfit.Message);
        }

        var save = await _store.SaveAsync(command.Scenario, cancellationToken);
        switch (save.Problem)
        {
            case ExpertScenarioSaveProblem.NoWave2End:
                return ScenarioErrors.Field("waves.wave2End", "A scenario is saved from the moment the end of wave 2 is placed: its id needs that month.");
            case ExpertScenarioSaveProblem.InvalidId:
                return ScenarioErrors.Field("id", "Not an id of an expert scenario.");
            case ExpertScenarioSaveProblem.UnreadableFile:
                return Error.Conflict("scenario.unreadable", "The file of " + command.Id + " cannot be read, so it is not overwritten. Repair or delete it.");
        }

        return Result.Success(await _responses.BuildAsync(save.Saved!, null, cancellationToken));
    }
}
