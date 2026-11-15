using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Assets.Queries;
using TradeViewer.Application.Features.Assets.Services;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Application.Features.Assets.Handlers;

public sealed class GetAssetQueryHandler : IQueryHandler<GetAssetQuery, AssetResponse>
{
    private readonly IAssetRepository _assets;
    private readonly AssetStatusReader _status;

    public GetAssetQueryHandler(IAssetRepository assets, AssetStatusReader status)
    {
        _assets = assets;
        _status = status;
    }

    public async Task<Result<AssetResponse>> HandleAsync(GetAssetQuery query, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(query.Source, query.Symbol, cancellationToken);
        if (asset == null)
        {
            return AssetErrors.NotFound(query.Source, query.Symbol);
        }

        return Result.Success(_status.Read(asset));
    }
}
