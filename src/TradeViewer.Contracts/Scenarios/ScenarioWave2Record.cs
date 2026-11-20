namespace TradeViewer.Contracts.Scenarios;

/// <summary>
/// The internals of wave 2 on the setup level: pattern zigzag, flat, expanded-flat, running-flat, triangle or
/// complex, with the ends of a, b and c. Without c the end of c is the end of wave 2.
/// </summary>
public sealed record ScenarioWave2Record(
    string? Timeframe,
    string? Pattern,
    ScenarioPointRecord? AEnd = null,
    ScenarioPointRecord? BEnd = null,
    ScenarioPointRecord? CEnd = null);
