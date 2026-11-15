using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Assets.Commands;
using TradeViewer.Application.Features.Assets.Services;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Application.Features.Assets.Handlers;

public sealed class ChangeAssetHistoryCommandHandler : ICommandHandler<ChangeAssetHistoryCommand, AssetResponse>
{
    private readonly IAssetRepository _assets;
    private readonly IDownloaderClient _downloader;
    private readonly IImportQueue _imports;
    private readonly AssetStatusReader _status;
    private readonly TimeProvider _time;

    public ChangeAssetHistoryCommandHandler(
        IAssetRepository assets,
        IDownloaderClient downloader,
        IImportQueue imports,
        AssetStatusReader status,
        TimeProvider time)
    {
        _assets = assets;
        _downloader = downloader;
        _imports = imports;
        _status = status;
        _time = time;
    }

    public async Task<Result<AssetResponse>> HandleAsync(ChangeAssetHistoryCommand command, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(command.Source, command.Symbol, cancellationToken);
        if (asset == null)
        {
            return AssetErrors.NotFound(command.Source, command.Symbol);
        }

        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var history = HistoryChange.Apply(asset.History, command.HistoryFrom, today);
        if (history.IsFailure)
        {
            return history.Error;
        }

        var sent = true;
        if (_downloader.Enabled)
        {
            var subscription = await _downloader.SubscribeAsync(asset.Source, asset.Symbol, history.Value, cancellationToken);
            if (subscription.IsFailure && subscription.Error.Type != ErrorType.Unavailable)
            {
                return subscription.Error;
            }

            // Not reachable: the change is kept and the sync sends the subscription again.
            sent = subscription.IsSuccess;
        }

        await _assets.SetHistoryAsync(asset.Source, asset.Symbol, history.Value, sent, cancellationToken);

        // The rescan imports what is wanted now and removes the candles before a later start.
        _imports.Rescan(asset.Source, asset.Symbol);

        var changed = await _assets.GetAsync(asset.Source, asset.Symbol, cancellationToken);
        return changed == null ? AssetErrors.NotFound(asset.Source, asset.Symbol) : Result.Success(_status.Read(changed));
    }
}
