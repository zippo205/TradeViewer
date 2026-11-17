namespace TradeViewer.Contracts.Drawings;

/// <summary>
/// A drawing as the API reads and writes it (GET /api/drawings, PUT /api/drawings/{id}), and as the expert
/// analysis file will hold it (decision 0007): it needs nothing of the workspace.
/// Tool, line style, degree and side are codes (trendLine, dashed, primary, above). Version is the payload
/// version of the tool. Levels, Wave, PointLabels and LineHidden belong to the tools that have them
/// (Fibonacci, wave label, polyline) and are null or false otherwise.
/// </summary>
public sealed record DrawingRecord(
    Guid Id,
    string Source,
    string Symbol,
    string Tool,
    int Version,
    bool Locked,
    IReadOnlyList<AnchorRecord> Anchors,
    DrawingStyleRecord Style,
    string? Text = null,
    IReadOnlyList<double>? Levels = null,
    WaveMarkRecord? Wave = null,
    IReadOnlyList<PointLabelRecord>? PointLabels = null,
    bool LineHidden = false);
