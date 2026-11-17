using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Drawings.Queries;
using TradeViewer.Application.Features.Drawings.Services;
using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Application.Features.Drawings.Handlers;

public sealed class GetDrawingsQueryHandler : IQueryHandler<GetDrawingsQuery, IReadOnlyList<DrawingRecord>>
{
    private readonly IAssetRepository _assets;
    private readonly IDrawingRepository _drawings;

    public GetDrawingsQueryHandler(IAssetRepository assets, IDrawingRepository drawings)
    {
        _assets = assets;
        _drawings = drawings;
    }

    public async Task<Result<IReadOnlyList<DrawingRecord>>> HandleAsync(GetDrawingsQuery query, CancellationToken cancellationToken)
    {
        if (await _assets.GetAsync(query.Source, query.Symbol, cancellationToken) == null)
        {
            return Error.NotFound("asset.notFound", "There is no asset " + query.Source + "/" + query.Symbol + ".");
        }

        var drawings = await _drawings.GetByAssetAsync(query.Source, query.Symbol, cancellationToken);
        return Result.Success<IReadOnlyList<DrawingRecord>>(drawings.Select(DrawingMapping.ToRecord).ToList());
    }
}
