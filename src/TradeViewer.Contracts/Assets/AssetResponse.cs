using TradeViewer.Contracts.MarketData;

namespace TradeViewer.Contracts.Assets;

/// <summary>
/// An asset with its state. Loading: its history is still being downloaded or imported (AS4).
/// SubscriptionPending: the Downloader has not accepted the current history yet; it is sent again.
/// Errors: files that broke the format rules and were not imported (MD6).
/// </summary>
public sealed record AssetResponse(
    string Source,
    string Symbol,
    string DisplayName,
    string AssetClass,
    double TickSize,
    int Position,
    bool Loading,
    bool SubscriptionPending,
    IReadOnlyList<AssetTimeframeResponse> Timeframes,
    IReadOnlyList<string> Errors);

/// <summary>
/// One timeframe of an asset, smallest first in the list. HistoryFrom: the first day wanted (yyyy-MM-dd), null
/// for the complete history. FirstCandle and LastCandle: what is stored. Chartable: its newest candles are there.
/// Gaps: those the source confirmed within the history, as the Downloader last reported them (MD4), oldest first;
/// empty without the Downloader.
/// </summary>
public sealed record AssetTimeframeResponse(
    string Timeframe,
    string? HistoryFrom,
    DateTimeOffset? FirstCandle,
    DateTimeOffset? LastCandle,
    int FilesImported,
    int FilesWanted,
    bool Loading,
    bool Chartable,
    IReadOnlyList<GapResponse> Gaps);
