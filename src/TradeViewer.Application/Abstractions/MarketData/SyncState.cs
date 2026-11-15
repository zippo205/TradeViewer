namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>How far the changes of the Downloader are imported for an asset, and the last status it reported.</summary>
public sealed record SyncState(long LastChange, string? LastStatusJson, long CheckedAt);
