namespace TradeViewer.Contracts.Scenarios;

/// <summary>The trade on the trigger level, for the verdicts trade-win and trade-loss.</summary>
public sealed record ScenarioTradeRecord(
    string? Timeframe,
    ScenarioPointRecord? Entry,
    ScenarioPriceRecord? Stop,
    ScenarioPointRecord? Add,
    IReadOnlyList<ScenarioPointRecord>? StopMoves,
    IReadOnlyList<ScenarioExitRecord>? Exits);
