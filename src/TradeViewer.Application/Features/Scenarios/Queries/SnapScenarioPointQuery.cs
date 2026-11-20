using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Queries;

/// <summary>Snaps a point the user placed or moved and answers with the changed scenario (SC10); nothing is written.</summary>
public sealed record SnapScenarioPointQuery(SnapScenarioPointRequest Request) : IQuery<ExpertScenarioResponse>;
