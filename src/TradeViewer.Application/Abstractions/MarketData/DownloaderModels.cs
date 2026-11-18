using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>What the Downloader says about a symbol of a source, and from when the source has data per timeframe.</summary>
public sealed record SymbolLookup(SymbolInfo Info, IReadOnlyDictionary<Timeframe, DateOnly> Earliest);

/// <summary>The answer to a subscription, per timeframe TradeViewer knows.</summary>
public sealed record SubscriptionResult(IReadOnlyList<TimeframeSubscription> Timeframes);

/// <summary>
/// Accepted false: the source does not provide the timeframe. Clipped: the wanted start lay before what
/// the source has; HistoryFrom is then the real start.
/// </summary>
public sealed record TimeframeSubscription(Timeframe Timeframe, bool Accepted, DateOnly? HistoryFrom, bool Clipped);

/// <summary>
/// The Downloader's state of a symbol. LastChange is what the sync polls. Subscribed: TradeViewer is among
/// the clients that subscribe the symbol (another client, e.g. EWTS, can keep it up to date as well).
/// </summary>
public sealed record SymbolStatus(long LastChange, bool Subscribed, IReadOnlyList<TimeframeStatus> Timeframes);

/// <summary>
/// Coverage of one timeframe. CoveredTo and the end of a gap are exclusive. LastChangedAt: when the Downloader last
/// wrote a file of it (DL2); added with Downloader Monitoring, so a status the sync stored before has none.
/// </summary>
public sealed record TimeframeStatus(
    Timeframe Timeframe,
    DateOnly? WantedFrom,
    DateTimeOffset? CoveredFrom,
    DateTimeOffset? CoveredTo,
    int PendingJobs,
    IReadOnlyList<CoverageGap> Gaps,
    string? LastError,
    DateTimeOffset? LastChangedAt = null);

/// <summary>Reason: missingUpstream, unavailable or pending.</summary>
public sealed record CoverageGap(DateTimeOffset From, DateTimeOffset To, string Reason);

/// <summary>
/// The files that changed after a change number, relative to the symbol folder. Complete false: the
/// Downloader no longer knows all changes, so every file of the symbol counts as changed.
/// </summary>
public sealed record SymbolChanges(long LastChange, bool Complete, IReadOnlyList<string> Files);

/// <summary>
/// An answer of the Downloader's symbol catalog (AS2). RefreshedAt null: the source has no list yet. Total counts
/// all matches; Truncated: more matched than Symbols holds, the user types more.
/// </summary>
public sealed record CatalogPage(DateTimeOffset? RefreshedAt, int Total, bool Truncated, IReadOnlyList<CatalogEntry> Symbols);

/// <summary>
/// A symbol of the catalog. SourceSymbol is the ticker for lookup and subscription; Symbol the folder name the
/// Downloader suggests; AssetClass as the Downloader writes it (crypto, stock, etf, forex); Exchange null for crypto
/// and forex. The catalog promises no candles: the lookup is the check.
/// </summary>
public sealed record CatalogEntry(string SourceSymbol, string Symbol, string DisplayName, string AssetClass, string? Exchange);

/// <summary>
/// The state of the Downloader as GET status reports it (DL1). State: running. LastCycle null before the first
/// cycle; NextCycleAt null when cycles are switched off. ConsistencyCheck null before the first weekly check.
/// </summary>
public sealed record DownloaderServiceStatus(
    string State,
    DateTimeOffset StartedAt,
    string Version,
    UpdateCycle? LastCycle,
    DateTimeOffset? NextCycleAt,
    DownloaderQueue Queue,
    IReadOnlyList<SourceState> Sources,
    IReadOnlyList<CatalogState> Catalogs,
    ConsistencyCheck? ConsistencyCheck);

/// <summary>One update cycle. Result: running, ok, partial, failed, interrupted, skipped. FinishedAt null while it runs.</summary>
public sealed record UpdateCycle(DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Result, int JobsPlanned, string? Message);

/// <summary>Jobs waiting per priority (recent, backfill, retry of missing data), running and failed.</summary>
public sealed record DownloaderQueue(int Recent, int Backfill, int RetryMissing, int Running, int Failed);

/// <summary>A source of the Downloader. State: ok, rateLimited (until RetryAt), unavailable (after 3 failed requests).</summary>
public sealed record SourceState(string Source, string State, DateTimeOffset? LastSuccessAt, string? LastError, DateTimeOffset? LastErrorAt, DateTimeOffset? RetryAt);

/// <summary>The symbol catalog of a source: last refresh (null before the first), symbols, the last failed refresh.</summary>
public sealed record CatalogState(string Source, DateTimeOffset? RefreshedAt, int SymbolCount, string? LastError, DateTimeOffset? LastErrorAt);

/// <summary>The last weekly check of the Downloader's files: missing, damaged or unknown files found.</summary>
public sealed record ConsistencyCheck(DateTimeOffset LastRunAt, int Problems);

/// <summary>
/// A symbol subscribed at the Downloader, merged over all clients (DL3). Timeframes: those TradeViewer knows, with the
/// earliest start any client wants (null: from the first available day) and the clients that want them.
/// </summary>
public sealed record MergedSubscription(
    string Source,
    string Symbol,
    string SourceSymbol,
    IReadOnlyList<string> SubscribedBy,
    IReadOnlyList<MergedTimeframe> Timeframes);

public sealed record MergedTimeframe(Timeframe Timeframe, DateOnly? WantedFrom, IReadOnlyList<string> SubscribedBy);
