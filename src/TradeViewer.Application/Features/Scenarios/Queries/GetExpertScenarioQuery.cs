using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Queries;

/// <summary>One expert scenario with its shapes, problems, levels and asset state (SC8, SC13: viewing writes nothing).</summary>
public sealed record GetExpertScenarioQuery(string Id) : IQuery<ExpertScenarioResponse>;
