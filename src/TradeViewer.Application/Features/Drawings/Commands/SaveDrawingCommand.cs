using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Application.Features.Drawings.Commands;

/// <summary>
/// Stores a drawing under its id, new or changed (PUT /api/drawings/{id}); repeating it changes nothing (WS1).
/// Window is the id of the window that made the change: it ignores its own DrawingsChanged event.
/// </summary>
public sealed record SaveDrawingCommand(Guid Id, DrawingRecord? Drawing, string? Window) : ICommand<Unit>;
