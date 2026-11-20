namespace TradeViewer.Contracts.Scenarios;

/// <summary>
/// An expert scenario as EWTS's format holds it (docs/knowledge/ewts-interface.md, "Expert Scenario Files"), plus
/// TradeViewer's own section Drawings (decision 0012). Id is null until the first save takes one (SC14). A file
/// may be incomplete (saved at every change), so every section and most fields may be missing. Fields the format
/// has and this record does not (legacy, later additions) are kept by the store in the file, never here.
/// </summary>
public sealed record ExpertScenarioRecord(
    string? Id,
    string Source,
    string Symbol,
    string ContextTimeframe,
    string Direction,
    string? Verdict,
    bool Draft,
    string? Reason,
    string? Notes,
    DateTimeOffset? From,
    DateTimeOffset? To,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    ScenarioWavesRecord? Waves,
    ScenarioWave1Record? Wave1,
    ScenarioWave2Record? Wave2,
    ScenarioTradeRecord? Trade,
    IReadOnlyList<ScenarioDrawingRecord>? Drawings);
