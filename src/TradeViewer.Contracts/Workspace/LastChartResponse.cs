namespace TradeViewer.Contracts.Workspace;

/// <summary>The chart last used: opening TradeViewer without an address opens it (CP3).</summary>
public sealed record LastChartResponse(string Source, string Symbol, string Timeframe);
