using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Drawings.Commands;

namespace TradeViewer.Application.Features.Drawings.Handlers;

/// <summary>Deletes a preset. Drawings made with it keep their levels (DR4); the last preset of a kind stays.</summary>
public sealed class DeleteFibonacciPresetCommandHandler : ICommandHandler<DeleteFibonacciPresetCommand, Unit>
{
    private readonly IFibonacciPresetRepository _presets;

    public DeleteFibonacciPresetCommandHandler(IFibonacciPresetRepository presets)
    {
        _presets = presets;
    }

    public async Task<Result<Unit>> HandleAsync(DeleteFibonacciPresetCommand command, CancellationToken cancellationToken)
    {
        var all = await _presets.GetAllAsync(cancellationToken);
        var preset = all.FirstOrDefault(candidate => candidate.Id == command.Id);
        if (preset == null)
        {
            return Error.NotFound("fibonacciPreset.notFound", "There is no preset " + command.Id + ".");
        }

        if (all.Count(candidate => candidate.Kind == preset.Kind) == 1)
        {
            return Error.Conflict("fibonacciPreset.lastOfKind", "The last preset of a kind cannot be deleted: the tool needs its levels.");
        }

        await _presets.DeleteAsync(command.Id, cancellationToken);
        return Result.Success();
    }
}
