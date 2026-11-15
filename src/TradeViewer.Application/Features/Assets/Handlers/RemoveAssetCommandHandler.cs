using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Assets.Commands;
using TradeViewer.Application.Features.Assets.Services;

namespace TradeViewer.Application.Features.Assets.Handlers;

public sealed class RemoveAssetCommandHandler : ICommandHandler<RemoveAssetCommand, Unit>
{
    private readonly IAssetRepository _assets;
    private readonly IDownloaderClient _downloader;
    private readonly IImportQueue _imports;

    public RemoveAssetCommandHandler(IAssetRepository assets, IDownloaderClient downloader, IImportQueue imports)
    {
        _assets = assets;
        _downloader = downloader;
        _imports = imports;
    }

    public async Task<Result<Unit>> HandleAsync(RemoveAssetCommand command, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(command.Source, command.Symbol, cancellationToken);
        if (asset == null)
        {
            return AssetErrors.NotFound(command.Source, command.Symbol);
        }

        // The epic EWTS Instances switches off the analysis of the asset here, before anything is deleted (EW3).

        // Removing works without the Downloader: an unsubscription that fails is remembered and sent again by the sync.
        var unsubscribePending = false;
        if (_downloader.Enabled)
        {
            var unsubscribed = await _downloader.UnsubscribeAsync(asset.Source, asset.Symbol, cancellationToken);
            unsubscribePending = unsubscribed.IsFailure;
        }

        await _assets.RemoveAsync(asset.Source, asset.Symbol, unsubscribePending, cancellationToken);
        _imports.Delete(asset.Source, asset.Symbol);

        return Result.Success();
    }
}
