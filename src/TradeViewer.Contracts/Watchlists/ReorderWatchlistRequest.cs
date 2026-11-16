namespace TradeViewer.Contracts.Watchlists;

/// <summary>Body of PUT /api/watchlists/{assetClass}/order: all assets of the list in their new order (AS1).</summary>
public sealed record ReorderWatchlistRequest(IReadOnlyList<WatchlistEntry> Assets);

public sealed record WatchlistEntry(string Source, string Symbol);
