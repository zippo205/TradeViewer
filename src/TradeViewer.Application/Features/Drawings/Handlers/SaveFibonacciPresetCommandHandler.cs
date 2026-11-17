using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Drawings.Commands;
using TradeViewer.Application.Features.Drawings.Services;
using TradeViewer.Application.Features.Drawings.Validators;
using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Application.Features.Drawings.Handlers;

/// <summary>Stores a preset (DR4); its levels are kept sorted. Changing its kind must leave another preset of the old kind.</summary>
public sealed class SaveFibonacciPresetCommandHandler : ICommandHandler<SaveFibonacciPresetCommand, FibonacciPresetRecord>
{
    private readonly IFibonacciPresetRepository _presets;
    private readonly SaveFibonacciPresetCommandValidator _validator = new SaveFibonacciPresetCommandValidator();

    public SaveFibonacciPresetCommandHandler(IFibonacciPresetRepository presets)
    {
        _presets = presets;
    }

    public async Task<Result<FibonacciPresetRecord>> HandleAsync(SaveFibonacciPresetCommand command, CancellationToken cancellationToken)
    {
        var invalid = _validator.Validate(command, out var preset);
        if (invalid != null)
        {
            return invalid;
        }

        var all = await _presets.GetAllAsync(cancellationToken);
        var existing = all.FirstOrDefault(candidate => candidate.Id == preset!.Id);
        if (existing != null && existing.Kind != preset!.Kind && all.Count(candidate => candidate.Kind == existing.Kind) == 1)
        {
            return Error.Conflict("fibonacciPreset.lastOfKind", "This is the only preset of its kind; its kind cannot change.");
        }

        await _presets.SaveAsync(preset!, cancellationToken);
        return Result.Success(DrawingMapping.ToRecord(preset!));
    }
}
