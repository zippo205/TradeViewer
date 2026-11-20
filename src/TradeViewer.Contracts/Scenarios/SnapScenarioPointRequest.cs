namespace TradeViewer.Contracts.Scenarios;

/// <summary>
/// A named point the user placed or let go (SC10), for the server to snap. Point is the field of the point in the
/// file (waves.wave1Start, wave1.iEnd, wave2.aEnd, trade.entry, trade.add, trade.stop, trade.stopMoves,
/// trade.exits). Index is the position of a stop move or an exit; its count or no index adds one. Time and Price
/// are where the pointer was. Kind (target or stop) is the kind of a new exit. Nothing is written.
/// </summary>
public sealed record SnapScenarioPointRequest(
    ExpertScenarioRecord Scenario,
    string Point,
    DateTimeOffset Time,
    double Price,
    int? Index = null,
    string? Kind = null);
