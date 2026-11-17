using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Drawings.Queries;
using TradeViewer.Application.Features.Drawings.Services;
using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Application.Features.Drawings.Handlers;

public sealed class GetFibonacciPresetsQueryHandler : IQueryHandler<GetFibonacciPresetsQuery, IReadOnlyList<FibonacciPresetRecord>>
{
    private readonly IFibonacciPresetRepository _presets;

    public GetFibonacciPresetsQueryHandler(IFibonacciPresetRepository presets)
    {
        _presets = presets;
    }

    public async Task<Result<IReadOnlyList<FibonacciPresetRecord>>> HandleAsync(GetFibonacciPresetsQuery query, CancellationToken cancellationToken)
    {
        var presets = await _presets.GetAllAsync(cancellationToken);
        return Result.Success<IReadOnlyList<FibonacciPresetRecord>>(presets.Select(DrawingMapping.ToRecord).ToList());
    }
}
