namespace TradeViewer.Contracts.Scenarios;

/// <summary>
/// The internals of wave 1 on the setup level. Pattern impulse or leading-diagonal has the points i to v, pattern
/// three the points a to c; a file never holds both. Without v or c the last point is the end of wave 1.
/// </summary>
public sealed record ScenarioWave1Record(
    string? Timeframe,
    string? Pattern,
    ScenarioPointRecord? IEnd = null,
    ScenarioPointRecord? IiEnd = null,
    ScenarioPointRecord? IiiEnd = null,
    ScenarioPointRecord? IvEnd = null,
    ScenarioPointRecord? VEnd = null,
    ScenarioPointRecord? AEnd = null,
    ScenarioPointRecord? BEnd = null,
    ScenarioPointRecord? CEnd = null);
