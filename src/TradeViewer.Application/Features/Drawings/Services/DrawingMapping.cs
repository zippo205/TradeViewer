using TradeViewer.Contracts.Drawings;
using TradeViewer.Domain.Drawings;

namespace TradeViewer.Application.Features.Drawings.Services;

/// <summary>Between the records of the API (codes, ISO times) and the Domain (enums, Unix seconds).</summary>
public static class DrawingMapping
{
    public static DrawingRecord ToRecord(Drawing drawing)
    {
        return new DrawingRecord(
            drawing.Id,
            drawing.Source,
            drawing.Symbol,
            DrawingCodes.Code(drawing.Tool),
            drawing.Version,
            drawing.Locked,
            drawing.Anchors.Select(anchor => new AnchorRecord(DateTimeOffset.FromUnixTimeSeconds(anchor.Time), anchor.Price)).ToList(),
            new DrawingStyleRecord(drawing.Style.Color, drawing.Style.Width, DrawingCodes.Code(drawing.Style.LineStyle), drawing.Style.Fill),
            drawing.Text,
            ToRecords(drawing.Levels),
            drawing.Wave == null ? null : ToRecord(drawing.Wave),
            drawing.PointLabels?.Select(label => new PointLabelRecord(label.Point, ToRecord(label.Mark))).ToList(),
            drawing.LineHidden);
    }

    /// <summary>
    /// The drawing the record describes, or null with the problems of the codes and parts that cannot be read.
    /// The rules of the drawing itself are checked afterwards (<see cref="DrawingRules"/>).
    /// </summary>
    public static Drawing? ToDomain(DrawingRecord record, Dictionary<string, string[]> problems)
    {
        if (!DrawingCodes.TryParse(record.Tool, out DrawingTool tool))
        {
            problems["tool"] = new[] { "Unknown tool " + record.Tool + "." };
        }

        if (record.Anchors == null || record.Anchors.Any(anchor => anchor == null))
        {
            problems["anchors"] = new[] { "The anchors are required." };
        }

        var lineStyle = LineStyle.Solid;
        if (record.Style == null)
        {
            problems["style"] = new[] { "The style is required." };
        }
        else if (!DrawingCodes.TryParse(record.Style.LineStyle, out lineStyle))
        {
            problems["style.lineStyle"] = new[] { "The line style is solid, dashed or dotted." };
        }

        WaveMark? wave = null;
        if (record.Wave != null)
        {
            wave = ToDomain(record.Wave);
            if (wave == null)
            {
                problems["wave"] = new[] { "Unknown degree or side." };
            }
        }

        List<PointLabel>? pointLabels = null;
        if (record.PointLabels != null)
        {
            pointLabels = new List<PointLabel>();
            foreach (var label in record.PointLabels)
            {
                var mark = label?.Mark == null ? null : ToDomain(label.Mark);
                if (mark == null)
                {
                    problems["pointLabels"] = new[] { "Unknown degree or side of a label." };
                    break;
                }

                pointLabels.Add(new PointLabel(label!.Point, mark));
            }
        }

        if (problems.Count > 0)
        {
            return null;
        }

        return new Drawing(
            record.Id,
            record.Source,
            record.Symbol,
            tool,
            record.Version,
            record.Locked,
            record.Anchors!.Select(anchor => new Anchor(anchor.Time.ToUnixTimeSeconds(), anchor.Price)).ToList(),
            new DrawingStyle(record.Style!.Color, record.Style.Width, lineStyle, record.Style.Fill),
            record.Text,
            ToDomain(record.Levels),
            wave,
            pointLabels,
            record.LineHidden);
    }

    public static FibonacciPresetRecord ToRecord(FibonacciPreset preset)
    {
        return new FibonacciPresetRecord(preset.Id, preset.Name, DrawingCodes.Code(preset.Kind), ToRecords(preset.Levels)!);
    }

    public static IReadOnlyList<FibonacciLevelRecord>? ToRecords(IReadOnlyList<FibonacciLevel>? levels)
    {
        return levels?.Select(level => new FibonacciLevelRecord(level.Ratio, level.Visible)).ToList();
    }

    /// <summary>The levels of a record; a missing level becomes a missing entry, which the rules refuse.</summary>
    public static IReadOnlyList<FibonacciLevel>? ToDomain(IReadOnlyList<FibonacciLevelRecord>? levels)
    {
        return levels?.Select(level => level == null ? null! : new FibonacciLevel(level.Ratio, level.Visible)).ToList();
    }

    private static WaveMarkRecord ToRecord(WaveMark mark)
    {
        return new WaveMarkRecord(DrawingCodes.Code(mark.Degree), mark.Label, DrawingCodes.Code(mark.Side));
    }

    /// <returns>Null when degree or side is unknown; the label is checked by the rules of the drawing.</returns>
    private static WaveMark? ToDomain(WaveMarkRecord record)
    {
        if (!DrawingCodes.TryParse(record.Degree, out WaveDegree degree) || !DrawingCodes.TryParse(record.Side, out WaveSide side))
        {
            return null;
        }

        return new WaveMark(degree, record.Label, side);
    }
}
