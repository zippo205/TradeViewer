namespace TradeViewer.Contracts.Overlays;

/// <summary>
/// A group of shapes in the vocabulary of EWTS's drawings (docs/knowledge/ewts-interface.md, "Drawings"): an analysis
/// overlay, a test-run scenario, or the shapes TradeViewer builds from the named points of an expert scenario. Key is
/// stable, so the hidden groups of a chart stay hidden. Level is set only for an expert scenario (SC21): the level
/// the group belongs to, context, setup or trigger.
/// </summary>
public sealed record OverlayGroupRecord(
    string Key,
    string Title,
    string Kind,
    string? Timeframe,
    IReadOnlyList<OverlayShapeRecord> Shapes,
    string? Level = null);
