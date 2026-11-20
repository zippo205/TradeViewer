using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Queries;

/// <summary>
/// The shapes and problems of a scenario as the editor holds it, without writing: after a change that is no snap
/// (a point removed, a field typed) of a scenario that has no id yet (SC14).
/// </summary>
public sealed record PreviewExpertScenarioQuery(ExpertScenarioRecord Scenario) : IQuery<ExpertScenarioResponse>;
