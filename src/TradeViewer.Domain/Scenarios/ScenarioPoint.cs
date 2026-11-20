namespace TradeViewer.Domain.Scenarios;

/// <summary>A point of a scenario: the open time of its candle in Unix seconds (UTC) and its price.</summary>
public readonly record struct ScenarioPoint(long Time, double Price);
