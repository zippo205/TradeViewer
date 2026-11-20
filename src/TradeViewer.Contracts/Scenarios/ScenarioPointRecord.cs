namespace TradeViewer.Contracts.Scenarios;

/// <summary>A named point of an expert scenario: the open time of its candle (UTC), its price and an optional reason.</summary>
public sealed record ScenarioPointRecord(DateTimeOffset Time, double Price, string? Why = null);
