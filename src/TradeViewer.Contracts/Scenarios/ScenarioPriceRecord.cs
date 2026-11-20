namespace TradeViewer.Contracts.Scenarios;

/// <summary>A price without a time, the first stop of a trade, with an optional reason.</summary>
public sealed record ScenarioPriceRecord(double Price, string? Why = null);
