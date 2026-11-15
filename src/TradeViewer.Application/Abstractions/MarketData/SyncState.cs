namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>
/// How far the changes of the Downloader are imported for an asset, the last status it reported (null before
/// the first answer) and when it was asked last (Unix seconds).
/// </summary>
public sealed record SyncState(long LastChange, SymbolStatus? LastStatus, long CheckedAt);
