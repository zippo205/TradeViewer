using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Assets.Commands;
using TradeViewer.Application.Features.Assets.Services;

namespace TradeViewer.Application.Features.Assets.Handlers;

public sealed class RebuildAssetCommandHandler : ICommandHandler<RebuildAssetCommand, Unit>
{
    private readonly IAssetRepository _assets;
    private readonly IImportQueue _imports;

    public RebuildAssetCommandHandler(IAssetRepository assets, IImportQueue imports)
    {
        _assets = assets;
        _imports = imports;
    }

    public async Task<Result<Unit>> HandleAsync(RebuildAssetCommand command, CancellationToken cancellationToken)
    {
        if (await _assets.GetAsync(command.Source, command.Symbol, cancellationToken) == null)
        {
            return AssetErrors.NotFound(command.Source, command.Symbol);
        }

        _imports.Rebuild(command.Source, command.Symbol);
        return Result.Success();
    }
}
