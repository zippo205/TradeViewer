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

/// <summary>Coverage of one timeframe. CoveredTo and the end of a gap are exclusive.</summary>
public sealed record TimeframeStatus(
    Timeframe Timeframe,
    DateOnly? WantedFrom,
    DateTimeOffset? CoveredFrom,
    DateTimeOffset? CoveredTo,
    int PendingJobs,
    IReadOnlyList<CoverageGap> Gaps,
    string? LastError);

/// <summary>Reason: missingUpstream, unavailable or pending.</summary>
public sealed record CoverageGap(DateTimeOffset From, DateTimeOffset To, string Reason);

/// <summary>
/// The files that changed after a change number, relative to the symbol folder. Complete false: the
/// Downloader no longer knows all changes, so every file of the symbol counts as changed.
/// </summary>
public sealed record SymbolChanges(long LastChange, bool Complete, IReadOnlyList<string> Files);
