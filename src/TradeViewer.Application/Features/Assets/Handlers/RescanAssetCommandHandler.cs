using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Assets.Commands;
using TradeViewer.Application.Features.Assets.Services;

namespace TradeViewer.Application.Features.Assets.Handlers;

public sealed class RescanAssetCommandHandler : ICommandHandler<RescanAssetCommand, Unit>
{
    private readonly IAssetRepository _assets;
    private readonly IImportQueue _imports;

    public RescanAssetCommandHandler(IAssetRepository assets, IImportQueue imports)
    {
        _assets = assets;
        _imports = imports;
    }

    public async Task<Result<Unit>> HandleAsync(RescanAssetCommand command, CancellationToken cancellationToken)
    {
        if (await _assets.GetAsync(command.Source, command.Symbol, cancellationToken) == null)
        {
            return AssetErrors.NotFound(command.Source, command.Symbol);
        }

        _imports.Rescan(command.Source, command.Symbol);
        return Result.Success();
    }
}
