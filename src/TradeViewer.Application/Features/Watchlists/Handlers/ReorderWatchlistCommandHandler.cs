using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Watchlists.Commands;
using TradeViewer.Application.Features.Watchlists.Validators;
using TradeViewer.Domain.Assets;

namespace TradeViewer.Application.Features.Watchlists.Handlers;

public sealed class ReorderWatchlistCommandHandler : ICommandHandler<ReorderWatchlistCommand, Unit>
{
    private readonly IAssetRepository _assets;
    private readonly ReorderWatchlistCommandValidator _validator = new ReorderWatchlistCommandValidator();

    public ReorderWatchlistCommandHandler(IAssetRepository assets)
    {
        _assets = assets;
    }

    public async Task<Result<Unit>> HandleAsync(ReorderWatchlistCommand command, CancellationToken cancellationToken)
    {
        if (!AssetCodes.TryParse(command.AssetClass, out AssetClass assetClass))
        {
            return Error.NotFound("watchlist.notFound", "There is no watchlist " + command.AssetClass + ".");
        }

        var invalid = _validator.Validate(command);
        if (invalid != null)
        {
            return invalid;
        }

        var order = command.Assets!.Select(asset => (asset.Source, asset.Symbol)).ToList();
        if (!await _assets.SetOrderAsync(assetClass, order, cancellationToken))
        {
            // Another window added or removed an asset meanwhile: the page reads the lists again.
            return Error.Conflict("watchlist.changed", "The watchlist has other assets than the new order names. Nothing was changed.");
        }

        return Result.Success();
    }
}
