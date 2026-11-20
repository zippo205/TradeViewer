using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Queries;

/// <summary>Whether the scenario pages are on and the editor can write (architecture, chapter 12).</summary>
public sealed record GetScenariosStatusQuery : IQuery<ScenariosStatusResponse>;
