using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Assets.Queries;
using TradeViewer.Application.Features.Assets.Services;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Application.Features.Assets.Handlers;

public sealed class GetAssetsQueryHandler : IQueryHandler<GetAssetsQuery, IReadOnlyList<AssetResponse>>
{
    private readonly IAssetRepository _assets;
    private readonly AssetStatusReader _status;

    public GetAssetsQueryHandler(IAssetRepository assets, AssetStatusReader status)
    {
        _assets = assets;
        _status = status;
    }

    public async Task<Result<IReadOnlyList<AssetResponse>>> HandleAsync(GetAssetsQuery query, CancellationToken cancellationToken)
    {
        var assets = await _assets.GetAllAsync(cancellationToken);
        IReadOnlyList<AssetResponse> responses = assets.Select(_status.Read).ToList();
        return Result.Success(responses);
    }
}
