using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Realtime;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Drawings.Commands;

namespace TradeViewer.Application.Features.Drawings.Handlers;

public sealed class DeleteDrawingCommandHandler : ICommandHandler<DeleteDrawingCommand, Unit>
{
    private readonly IDrawingRepository _drawings;
    private readonly IWorkspaceNotifier _notifier;

    public DeleteDrawingCommandHandler(IDrawingRepository drawings, IWorkspaceNotifier notifier)
    {
        _drawings = drawings;
        _notifier = notifier;
    }

    public async Task<Result<Unit>> HandleAsync(DeleteDrawingCommand command, CancellationToken cancellationToken)
    {
        var drawing = await _drawings.GetAsync(command.Id, cancellationToken);
        if (drawing == null || !await _drawings.DeleteAsync(command.Id, cancellationToken))
        {
            // Deleted already, e.g. a delete repeated after a lost answer: the client treats it as done.
            return Error.NotFound("drawing.notFound", "There is no drawing " + command.Id + ".");
        }

        await _notifier.DrawingsChangedAsync(drawing.Source, drawing.Symbol, command.Window ?? "");
        return Result.Success();
    }
}
