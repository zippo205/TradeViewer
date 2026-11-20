using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Contracts.Scenarios;

/// <summary>
/// A free drawing of an expert scenario (SC20, decision 0012): the drawing record of the workspace without the
/// asset (the scenario has it) plus its level, context, setup or trigger. EWTS ignores these drawings.
/// </summary>
public sealed record ScenarioDrawingRecord(
    Guid Id,
    string Level,
    string Tool,
    int Version,
    bool Locked,
    IReadOnlyList<AnchorRecord> Anchors,
    DrawingStyleRecord Style,
    string? Text = null,
    IReadOnlyList<FibonacciLevelRecord>? Levels = null,
    WaveMarkRecord? Wave = null,
    IReadOnlyList<PointLabelRecord>? PointLabels = null,
    bool LineHidden = false);
