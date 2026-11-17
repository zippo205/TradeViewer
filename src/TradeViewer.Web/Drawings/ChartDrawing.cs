using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Web.Drawings;

/// <summary>A point of a drawing as the chart module holds it: time in Unix seconds (UTC).</summary>
public sealed record ChartAnchor(long Time, double? Price);

/// <summary>
/// A drawing as it passes to and from the chart module: the record of the Api with times in Unix seconds. A
/// drawing the module made itself has no source, symbol and version yet; <see cref="ChartDrawings.ToRecord"/>
/// gives it those of the asset shown.
/// </summary>
public sealed record ChartDrawing(
    string Id,
    string Tool,
    bool Locked,
    IReadOnlyList<ChartAnchor> Anchors,
    DrawingStyleRecord Style,
    string? Text = null,
    int? Version = null,
    string? Source = null,
    string? Symbol = null,
    IReadOnlyList<double>? Levels = null,
    WaveMarkRecord? Wave = null,
    IReadOnlyList<PointLabelRecord>? PointLabels = null,
    bool? LineHidden = null);

public static class ChartDrawings
{
    /// <summary>The payload version of every tool a new drawing is written with (DrawingTools.PayloadVersion).</summary>
    public const int NewVersion = 1;

    public static ChartDrawing ToChart(DrawingRecord drawing)
    {
        return new ChartDrawing(
            drawing.Id.ToString(),
            drawing.Tool,
            drawing.Locked,
            drawing.Anchors.Select(anchor => new ChartAnchor(anchor.Time.ToUnixTimeSeconds(), anchor.Price)).ToList(),
            drawing.Style,
            drawing.Text,
            drawing.Version,
            drawing.Source,
            drawing.Symbol,
            drawing.Levels,
            drawing.Wave,
            drawing.PointLabels,
            drawing.LineHidden);
    }

    /// <returns>The record, or null when the id from the module is not a UUID.</returns>
    public static DrawingRecord? ToRecord(ChartDrawing drawing, string source, string symbol)
    {
        if (!Guid.TryParse(drawing.Id, out var id))
        {
            return null;
        }

        return new DrawingRecord(
            id,
            drawing.Source ?? source,
            drawing.Symbol ?? symbol,
            drawing.Tool,
            drawing.Version ?? NewVersion,
            drawing.Locked,
            drawing.Anchors.Select(anchor => new AnchorRecord(DateTimeOffset.FromUnixTimeSeconds(anchor.Time), anchor.Price)).ToList(),
            drawing.Style,
            drawing.Text,
            drawing.Levels,
            drawing.Wave,
            drawing.PointLabels,
            drawing.LineHidden ?? false);
    }
}
