namespace TradeViewer.Contracts.Downloader;

/// <summary>
/// All subscriptions of the Downloader, one per symbol merged over its clients (DL3). OwnClient: the client name of
/// this TradeViewer; the subscriptions of other clients are read-only here.
/// </summary>
public sealed record DownloaderSubscriptionsResponse(string OwnClient, IReadOnlyList<DownloaderSubscriptionResponse> Subscriptions);

/// <summary>
/// Own: TradeViewer is among the clients. InWorkspace: the symbol is an asset of the workspace. Timeframes: those
/// TradeViewer knows, smallest first; WantedFrom the earliest start any client wants (yyyy-MM-dd), null complete.
/// </summary>
public sealed record DownloaderSubscriptionResponse(
    string Source,
    string Symbol,
    string SourceSymbol,
    IReadOnlyList<string> SubscribedBy,
    bool Own,
    bool InWorkspace,
    IReadOnlyList<DownloaderSubscribedTimeframeResponse> Timeframes);

public sealed record DownloaderSubscribedTimeframeResponse(string Timeframe, string? WantedFrom, IReadOnlyList<string> SubscribedBy);
