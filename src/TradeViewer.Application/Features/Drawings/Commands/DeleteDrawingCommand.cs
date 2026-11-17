using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Drawings.Commands;

/// <summary>Deletes a drawing (DR2). Window is the id of the window that made the change.</summary>
public sealed record DeleteDrawingCommand(Guid Id, string? Window) : ICommand<Unit>;
