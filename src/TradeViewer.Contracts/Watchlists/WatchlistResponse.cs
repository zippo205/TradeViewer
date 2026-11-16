namespace TradeViewer.Contracts.Watchlists;

/// <summary>One of the five fixed watchlists (AS1): the assets of one asset class in the order of the user.</summary>
public sealed record WatchlistResponse(string AssetClass, string Title, IReadOnlyList<WatchlistRowResponse> Rows);

/// <summary>
/// A row of a watchlist (AS6). LastClose and LastCloseTime: the newest closed candle of the smallest
/// timeframe, null without candles. ChangePercent: against the previous daily close, null when there is none.
/// Warning: why the asset needs a look (a file was not imported, the source has a gap), or null.
/// Timeframes: the codes that can be charted. AnalysedBy: the EWTS instances that analyse the asset.
/// </summary>
public sealed record WatchlistRowResponse(
    string Source,
    string Symbol,
    string DisplayName,
    double TickSize,
    double? LastClose,
    DateTimeOffset? LastCloseTime,
    double? ChangePercent,
    bool Loading,
    string? Warning,
    IReadOnlyList<string> Timeframes,
    IReadOnlyList<string> AnalysedBy);
