using TradeViewer.Domain.Assets;

namespace TradeViewer.Application.Abstractions.Workspace;

/// <summary>The assets of the user in workspace.db; they survive a rebuild of the candle store (WS2).</summary>
public interface IAssetRepository
{
    /// <summary>All assets, ordered by asset class and position.</summary>
    Task<IReadOnlyList<Asset>> GetAllAsync(CancellationToken cancellationToken);

    Task<Asset?> GetAsync(string source, string symbol, CancellationToken cancellationToken);

    /// <summary>Stores a new asset at the end of its watchlist.</summary>
    /// <returns>The stored asset, or null when it exists already.</returns>
    Task<Asset?> AddAsync(SymbolInfo info, AssetHistory history, CancellationToken cancellationToken);

    /// <summary>Replaces the history of an asset. With subscriptionSent false the sync sends the subscription again.</summary>
    Task SetHistoryAsync(string source, string symbol, AssetHistory history, bool subscriptionSent, CancellationToken cancellationToken);

    Task SetSubscriptionSentAsync(string source, string symbol, CancellationToken cancellationToken);

    /// <summary>The assets whose subscription the Downloader has not accepted yet in its current form.</summary>
    Task<IReadOnlyList<Asset>> GetUnsentSubscriptionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the asset and what the user created for it. With unsubscribePending the asset is remembered
    /// until the Downloader has confirmed the end of its subscription.
    /// </summary>
    /// <returns>False when there was no such asset.</returns>
    Task<bool> RemoveAsync(string source, string symbol, bool unsubscribePending, CancellationToken cancellationToken);

    /// <summary>Removed assets whose subscription at the Downloader still has to be ended, as (source, symbol).</summary>
    Task<IReadOnlyList<(string Source, string Symbol)>> GetPendingUnsubscriptionsAsync(CancellationToken cancellationToken);

    Task ClearPendingUnsubscriptionAsync(string source, string symbol, CancellationToken cancellationToken);
}
