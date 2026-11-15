using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Assets.Commands;
using TradeViewer.Application.Features.Assets.Services;
using TradeViewer.Contracts.Assets;
using TradeViewer.Domain.Assets;

namespace TradeViewer.Application.Features.Assets.Handlers;

public sealed class AddAssetCommandHandler : ICommandHandler<AddAssetCommand, AssetResponse>
{
    private readonly ISymbolCatalog _catalog;
    private readonly IAssetRepository _assets;
    private readonly IHistorySettingsRepository _historySettings;
    private readonly IDownloaderClient _downloader;
    private readonly IAssetFolders _folders;
    private readonly IImportQueue _imports;
    private readonly AssetStatusReader _status;
    private readonly TimeProvider _time;

    public AddAssetCommandHandler(
        ISymbolCatalog catalog,
        IAssetRepository assets,
        IHistorySettingsRepository historySettings,
        IDownloaderClient downloader,
        IAssetFolders folders,
        IImportQueue imports,
        AssetStatusReader status,
        TimeProvider time)
    {
        _catalog = catalog;
        _assets = assets;
        _historySettings = historySettings;
        _downloader = downloader;
        _folders = folders;
        _imports = imports;
        _status = status;
        _time = time;
    }

    public async Task<Result<AssetResponse>> HandleAsync(AddAssetCommand command, CancellationToken cancellationToken)
    {
        // The user picks from the symbols of the source (AS2).
        var listed = _catalog.Find(command.Source, command.Symbol);
        if (listed == null)
        {
            return AssetErrors.UnknownSymbol(command.Source, command.Symbol);
        }

        if (await _assets.GetAsync(listed.Source, listed.Symbol, cancellationToken) != null)
        {
            return AssetErrors.AlreadyExists(listed.Source, listed.Symbol);
        }

        SymbolInfo info;
        if (_downloader.Enabled)
        {
            // The Downloader confirms the symbol; its answer is the metadata of the asset.
            var lookup = await _downloader.LookupAsync(listed.Source, listed.Symbol, cancellationToken);
            if (lookup.IsFailure)
            {
                return lookup.Error;
            }

            info = lookup.Value.Info;
        }
        else
        {
            // Without the Downloader TradeViewer works on the files alone.
            if (!_folders.HasFolder(listed.Source, listed.Symbol))
            {
                return AssetErrors.NoData(listed.Source, listed.Symbol);
            }

            info = listed;
        }

        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var history = (await _historySettings.GetAsync(cancellationToken)).StartingAt(today, info.SourceTimeframes);

        if (_downloader.Enabled)
        {
            // Nothing is stored unless the Downloader accepted the subscription.
            var subscription = await _downloader.SubscribeAsync(info.Source, info.Symbol, history, cancellationToken);
            if (subscription.IsFailure)
            {
                return subscription.Error;
            }
        }

        var asset = await _assets.AddAsync(info, history, cancellationToken);
        if (asset == null)
        {
            return AssetErrors.AlreadyExists(info.Source, info.Symbol);
        }

        _imports.Rescan(asset.Source, asset.Symbol);
        return Result.Success(_status.Read(asset));
    }
}
