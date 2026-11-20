namespace TradeViewer.Contracts.Scenarios;

/// <summary>The waves 1 and 2 on the context level: wave 1 start, wave 1 end and wave 2 end.</summary>
public sealed record ScenarioWavesRecord(
    string? Timeframe,
    ScenarioPointRecord? Wave1Start,
    ScenarioPointRecord? Wave1End,
    ScenarioPointRecord? Wave2End);
