namespace TradeViewer.Contracts.Scenarios;

/// <summary>An exit of a trade: the open time of the candle that reaches the level, the level's price, the share in percent and the kind (target or stop).</summary>
public sealed record ScenarioExitRecord(DateTimeOffset Time, double Price, double Share, string Kind, string? Why = null);
