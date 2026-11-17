using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Domain.Assets;

namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>
/// The control API of the Downloader (docs/knowledge/downloader-interface.md). Every call returns a result:
/// InvalidData when the source does not know the symbol (with the Downloader's message), NotFound,
/// Validation, and Unavailable when the Downloader or its source cannot be reached. Nothing throws.
/// </summary>
public interface IDownloaderClient
{
    /// <summary>False: the Downloader is switched off in the configuration and must not be called.</summary>
    bool Enabled { get; }

    /// <summary>Searches the symbol catalog of a source by the start of the ticker or any part of the name (AS2).</summary>
    Task<Result<CatalogPage>> SearchAsync(string source, string text, int limit, CancellationToken cancellationToken);

    /// <summary>Looks a symbol up at its source by its ticker, without subscribing it.</summary>
    Task<Result<SymbolLookup>> LookupAsync(string source, string ticker, CancellationToken cancellationToken);

    /// <summary>
    /// Subscribes the asset with its history, replacing an earlier subscription of TradeViewer to it: the symbol (the
    /// folder name) in the path, the ticker in the body.
    /// </summary>
    Task<Result<SubscriptionResult>> SubscribeAsync(string source, string symbol, string ticker, AssetHistory history, CancellationToken cancellationToken);

    /// <summary>Ends the subscription of TradeViewer. No subscription at the Downloader counts as done.</summary>
    Task<Result<Unit>> UnsubscribeAsync(string source, string symbol, CancellationToken cancellationToken);

    /// <summary>The symbols TradeViewer itself subscribes at the Downloader, as (source, symbol). Other clients are not in it.</summary>
    Task<Result<IReadOnlyList<(string Source, string Symbol)>>> GetOwnSubscriptionsAsync(CancellationToken cancellationToken);

    Task<Result<SymbolStatus>> GetSymbolStatusAsync(string source, string symbol, CancellationToken cancellationToken);

    Task<Result<SymbolChanges>> GetChangesAsync(string source, string symbol, long after, CancellationToken cancellationToken);
}
