using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Scenarios;

/// <summary>
/// What the form checks see of an expert scenario (SC15): its head and its named points, times in Unix seconds. The
/// Application builds it from the file's record. A section that is absent in the file is false in Has...; a point
/// that is absent is missing from <see cref="Points"/>.
/// </summary>
public sealed record ExpertScenarioReading(
    Timeframe? ContextTimeframe,
    string? Direction,
    string? Verdict,
    string? Reason,
    long? From,
    long? To,
    IReadOnlyDictionary<ScenarioPointName, ScenarioPoint> Points,
    bool HasWave1,
    string? Wave1Pattern,
    bool HasWave2,
    string? Wave2Pattern,
    bool HasTrade,
    double? StopPrice,
    IReadOnlyList<ScenarioPoint> StopMoves,
    IReadOnlyList<ScenarioExit> Exits);
