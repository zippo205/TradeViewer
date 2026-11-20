using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Commands;

/// <summary>
/// Writes an expert scenario (SC13). Id null: the first save, which takes the id (SC14); the scenario must have none.
/// Otherwise the id of the address, which the scenario must carry.
/// </summary>
public sealed record SaveExpertScenarioCommand(string? Id, ExpertScenarioRecord Scenario) : ICommand<ExpertScenarioResponse>;
