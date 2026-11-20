using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Scenarios.Commands;

/// <summary>Deletes an expert scenario; the page has asked first (SC16).</summary>
public sealed record DeleteExpertScenarioCommand(string Id) : ICommand<Unit>;
