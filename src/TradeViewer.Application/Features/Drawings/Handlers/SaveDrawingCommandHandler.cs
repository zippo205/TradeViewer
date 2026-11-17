using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Realtime;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Drawings.Commands;
using TradeViewer.Application.Features.Drawings.Validators;

namespace TradeViewer.Application.Features.Drawings.Handlers;

/// <summary>
/// Stores a drawing at once (WS1). The lock of a drawing (DR2) is kept by the chart, not here: the client
/// sends only the last of several changes of one drawing, so "unlock, then move" arrives as one save that
/// must not be refused.
/// </summary>
public sealed class SaveDrawingCommandHandler : ICommandHandler<SaveDrawingCommand, Unit>
{
    private readonly IDrawingRepository _drawings;
    private readonly IWorkspaceNotifier _notifier;
    private readonly SaveDrawingCommandValidator _validator = new SaveDrawingCommandValidator();

    public SaveDrawingCommandHandler(IDrawingRepository drawings, IWorkspaceNotifier notifier)
    {
        _drawings = drawings;
        _notifier = notifier;
    }

    public async Task<Result<Unit>> HandleAsync(SaveDrawingCommand command, CancellationToken cancellationToken)
    {
        var invalid = _validator.Validate(command, out var drawing);
        if (invalid != null)
        {
            return invalid;
        }

        var result = await _drawings.SaveAsync(drawing!, cancellationToken);
        switch (result)
        {
            case DrawingSaveResult.UnknownAsset:
                return Error.NotFound("asset.notFound", "There is no asset " + drawing!.Source + "/" + drawing.Symbol + ".");
            case DrawingSaveResult.OtherAsset:
                return Error.Conflict("drawing.otherAsset", "The id belongs to a drawing of another asset. Nothing was changed.");
        }

        await _notifier.DrawingsChangedAsync(drawing!.Source, drawing.Symbol, command.Window ?? "");
        return Result.Success();
    }
}
