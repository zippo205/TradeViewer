namespace TradeViewer.Contracts.Overlays;

/// <summary>
/// One shape of a group. Type says which fields it has:
/// line (Points, two or more, drawn as connected segments), label (Time, Price, Text, Side above or below),
/// level (Price, From, To, Text; To null: open to the right), zone (Lower, Upper, From, To, Text),
/// marker (Time, Price, Text), verticalLine (Time, Text).
/// Role chooses the colour (OV6). Detail is shown when the user points at the shape; Style is solid, dotted or
/// dashed, Weight normal or strong.
/// </summary>
public sealed record OverlayShapeRecord(
    string Type,
    string Role,
    IReadOnlyList<OverlayPointRecord>? Points = null,
    DateTimeOffset? Time = null,
    double? Price = null,
    string? Text = null,
    string? Side = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    double? Lower = null,
    double? Upper = null,
    string? Detail = null,
    string? Style = null,
    string? Weight = null);
