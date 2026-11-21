using TradeViewer.Contracts.Drawings;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Web.Drawings;

namespace TradeViewer.Web.Scenarios;

/// <summary>
/// The free drawings of an expert scenario (SC20, decision 0012): the records of the file's drawings section, each
/// with its level, as the chart module and the bar of the selected drawing take them. A drawing of another level than
/// the one shown is passed locked, so it is seen but not moved; it keeps its own lock in the file.
/// </summary>
public static class ScenarioDrawings
{
    public static ChartDrawing ToChart(ScenarioDrawingRecord drawing, bool editable)
    {
        return new ChartDrawing(
            drawing.Id.ToString(),
            drawing.Tool,
            drawing.Locked || !editable,
            drawing.Anchors.Select(anchor => new ChartAnchor(anchor.Time.ToUnixTimeSeconds(), anchor.Price)).ToList(),
            drawing.Style,
            drawing.Text,
            drawing.Version,
            null,
            null,
            drawing.Levels,
            drawing.Wave,
            drawing.PointLabels,
            drawing.LineHidden);
    }

    /// <returns>The record on the level, or null when the id from the module is not a UUID.</returns>
    public static ScenarioDrawingRecord? ToRecord(ChartDrawing drawing, string level)
    {
        if (!Guid.TryParse(drawing.Id, out var id))
        {
            return null;
        }

        return new ScenarioDrawingRecord(
            id,
            level,
            drawing.Tool,
            drawing.Version ?? ChartDrawings.NewVersion,
            drawing.Locked,
            drawing.Anchors.Select(anchor => new AnchorRecord(DateTimeOffset.FromUnixTimeSeconds(anchor.Time), anchor.Price)).ToList(),
            drawing.Style,
            drawing.Text,
            drawing.Levels,
            drawing.Wave,
            drawing.PointLabels,
            drawing.LineHidden ?? false);
    }

    /// <summary>The drawing as the bar of the selected drawing edits it (DrawingBar takes the workspace's record).</summary>
    public static DrawingRecord ToBar(ScenarioDrawingRecord drawing, string source, string symbol)
    {
        return new DrawingRecord(
            drawing.Id, source, symbol, drawing.Tool, drawing.Version, drawing.Locked, drawing.Anchors, drawing.Style,
            drawing.Text, drawing.Levels, drawing.Wave, drawing.PointLabels, drawing.LineHidden);
    }

    /// <summary>What the bar changed, back on the drawing's own level.</summary>
    public static ScenarioDrawingRecord FromBar(DrawingRecord drawing, string level)
    {
        return new ScenarioDrawingRecord(
            drawing.Id, level, drawing.Tool, drawing.Version, drawing.Locked, drawing.Anchors, drawing.Style,
            drawing.Text, drawing.Levels, drawing.Wave, drawing.PointLabels, drawing.LineHidden);
    }

    public static ScenarioDrawingRecord? Find(ExpertScenarioRecord scenario, Guid? id)
    {
        return id == null ? null : scenario.Drawings?.FirstOrDefault(drawing => drawing.Id == id);
    }

    /// <summary>The scenario with the drawing added, or replacing the one with its id (a drawing keeps its place in the list).</summary>
    public static ExpertScenarioRecord With(ExpertScenarioRecord scenario, ScenarioDrawingRecord drawing)
    {
        var drawings = (scenario.Drawings ?? Array.Empty<ScenarioDrawingRecord>()).ToList();
        var at = drawings.FindIndex(existing => existing.Id == drawing.Id);
        if (at >= 0)
        {
            drawings[at] = drawing;
        }
        else
        {
            drawings.Add(drawing);
        }

        return scenario with { Drawings = drawings };
    }

    public static ExpertScenarioRecord Without(ExpertScenarioRecord scenario, Guid id)
    {
        var drawings = (scenario.Drawings ?? Array.Empty<ScenarioDrawingRecord>()).Where(drawing => drawing.Id != id).ToList();
        return scenario with { Drawings = drawings.Count == 0 ? null : drawings };
    }
}
