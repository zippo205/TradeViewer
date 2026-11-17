namespace TradeViewer.Domain.Drawings;

/// <summary>
/// A drawing of the user on an asset (DR1 to DR5). It belongs to the asset, not to a timeframe (DR3), and
/// refers to market data by time only. The id comes from the client, so a save can be repeated (NF3, WS1).
/// Version is the payload version of the tool the drawing was written with (<see cref="DrawingTools.PayloadVersion"/>).
/// <para>What only some tools have:</para>
/// <list type="bullet">
/// <item>Levels: the ratios of a Fibonacci drawing, copied from the preset chosen when it was drawn (DR4).</item>
/// <item>Wave: degree, label and side of a wave label (DR5).</item>
/// <item>PointLabels and LineHidden: the wave labels on the points of a polyline, and a polyline that shows only its labels (DR5).</item>
/// </list>
/// <see cref="DrawingRules"/> says whether a drawing is valid.
/// </summary>
public sealed record Drawing(
    Guid Id,
    string Source,
    string Symbol,
    DrawingTool Tool,
    int Version,
    bool Locked,
    IReadOnlyList<Anchor> Anchors,
    DrawingStyle Style,
    string? Text = null,
    IReadOnlyList<FibonacciLevel>? Levels = null,
    WaveMark? Wave = null,
    IReadOnlyList<PointLabel>? PointLabels = null,
    bool LineHidden = false);
