namespace TradeViewer.Domain.Scenarios;

/// <summary>An exit of a trade: the open time of its candle, the price, the share in percent and the kind code.</summary>
public readonly record struct ScenarioExit(long Time, double Price, double Share, string? Kind);
