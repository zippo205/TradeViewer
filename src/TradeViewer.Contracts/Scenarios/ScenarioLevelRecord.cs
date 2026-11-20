namespace TradeViewer.Contracts.Scenarios;

/// <summary>A level of an expert scenario (context, setup, trigger) and its timeframe code (SC11).</summary>
public sealed record ScenarioLevelRecord(string Level, string Timeframe);
