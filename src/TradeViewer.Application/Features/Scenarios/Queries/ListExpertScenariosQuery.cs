using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Queries;

/// <summary>The heads of all expert scenarios, and the files that cannot be read with their error (SC7).</summary>
public sealed record ListExpertScenariosQuery : IQuery<IReadOnlyList<ExpertScenarioSummary>>;
