using System.Text.RegularExpressions;

namespace TradeViewer.Domain.Drawings;

/// <summary>
/// Whether a drawing can be stored: a known tool and payload version, the right number of anchors with
/// finite numbers, a valid style, and only the parts its tool has. What cannot be read is refused rather
/// than stored (priority 1: a stored drawing must always be readable again).
/// </summary>
public static partial class DrawingRules
{
    /// <summary>The latest anchor time accepted: 2200-01-01 UTC. Anchors may lie after the last candle (DR3), not in another age.</summary>
    public const long LatestTime = 7_258_118_400;

    /// <summary>The problems of a drawing, by field; empty when it is valid.</summary>
    public static IReadOnlyDictionary<string, string[]> Check(Drawing drawing)
    {
        var problems = new Dictionary<string, string[]>();

        if (drawing.Id == Guid.Empty)
        {
            problems["id"] = new[] { "The drawing needs an id." };
        }

        if (string.IsNullOrWhiteSpace(drawing.Source) || string.IsNullOrWhiteSpace(drawing.Symbol))
        {
            problems["asset"] = new[] { "The drawing needs the source and symbol of its asset." };
        }

        if (!Enum.IsDefined(drawing.Tool))
        {
            problems["tool"] = new[] { "Unknown tool." };
            return problems; // the other rules depend on the tool
        }

        var tool = drawing.Tool;
        if (drawing.Version < 1 || drawing.Version > DrawingTools.PayloadVersion(tool))
        {
            problems["version"] = new[] { $"Payload version {drawing.Version} of this tool is not known to this version of TradeViewer." };
        }

        CheckAnchors(drawing, problems);
        CheckStyle(drawing, problems);
        CheckText(drawing, problems);
        CheckParts(drawing, problems);

        return problems;
    }

    public static bool IsColor(string? color)
    {
        return color != null && ColorPattern().IsMatch(color);
    }

    private static void CheckAnchors(Drawing drawing, Dictionary<string, string[]> problems)
    {
        var anchors = drawing.Anchors;
        var (min, max) = DrawingTools.AnchorCount(drawing.Tool);
        if (anchors == null || anchors.Count < min || anchors.Count > max)
        {
            var wanted = min == max ? $"{min}" : $"{min} to {max}";
            problems["anchors"] = new[] { $"A {DrawingCodes.Code(drawing.Tool)} has {wanted} anchors." };
            return;
        }

        var withoutPrice = drawing.Tool == DrawingTool.WaveLabel;
        foreach (var anchor in anchors)
        {
            if (anchor.Time < 0 || anchor.Time > LatestTime)
            {
                problems["anchors"] = new[] { "An anchor time lies outside 1970 to 2200." };
                return;
            }

            if (withoutPrice && anchor.Price != null)
            {
                problems["anchors"] = new[] { "A wave label has no price: it sits at the high or low of its candle." };
                return;
            }

            if (!withoutPrice && (anchor.Price == null || !double.IsFinite(anchor.Price.Value)))
            {
                problems["anchors"] = new[] { "Every anchor needs a price that is a finite number." };
                return;
            }
        }
    }

    private static void CheckStyle(Drawing drawing, Dictionary<string, string[]> problems)
    {
        var style = drawing.Style;
        if (style == null)
        {
            problems["style"] = new[] { "The drawing needs a style." };
            return;
        }

        if (!IsColor(style.Color))
        {
            problems["style.color"] = new[] { "The colour is written #RRGGBB or #RRGGBBAA." };
        }

        if (style.Width < 1 || style.Width > 4)
        {
            problems["style.width"] = new[] { "The width is 1 to 4 pixels." };
        }

        if (!Enum.IsDefined(style.LineStyle))
        {
            problems["style.lineStyle"] = new[] { "The line style is solid, dashed or dotted." };
        }

        if (style.Fill != null && !DrawingTools.HasArea(drawing.Tool))
        {
            problems["style.fill"] = new[] { "Only a rectangle or a parallel channel has a fill." };
        }
        else if (style.Fill != null && !IsColor(style.Fill))
        {
            problems["style.fill"] = new[] { "The fill is written #RRGGBB or #RRGGBBAA." };
        }
    }

    private static void CheckText(Drawing drawing, Dictionary<string, string[]> problems)
    {
        if (drawing.Text != null && drawing.Text.Length > DrawingTools.MaxTextLength)
        {
            problems["text"] = new[] { $"The text may have at most {DrawingTools.MaxTextLength} characters." };
        }
        else if (drawing.Tool == DrawingTool.TextNote && string.IsNullOrWhiteSpace(drawing.Text))
        {
            problems["text"] = new[] { "A text note needs a text." };
        }
    }

    private static void CheckParts(Drawing drawing, Dictionary<string, string[]> problems)
    {
        var tool = drawing.Tool;

        if (DrawingTools.IsFibonacci(tool))
        {
            var levelProblem = FibonacciPresets.CheckLevels(drawing.Levels);
            if (levelProblem != null)
            {
                problems["levels"] = new[] { levelProblem };
            }
        }
        else if (drawing.Levels != null)
        {
            problems["levels"] = new[] { "Only a Fibonacci drawing has levels." };
        }

        if (tool == DrawingTool.WaveLabel)
        {
            var markProblem = CheckMark(drawing.Wave);
            if (markProblem != null)
            {
                problems["wave"] = new[] { markProblem };
            }
        }
        else if (drawing.Wave != null)
        {
            problems["wave"] = new[] { "Only a wave label has a degree and a label." };
        }

        if (tool == DrawingTool.Polyline)
        {
            var labelProblem = CheckPointLabels(drawing.PointLabels, drawing.Anchors?.Count ?? 0);
            if (labelProblem != null)
            {
                problems["pointLabels"] = new[] { labelProblem };
            }
        }
        else
        {
            if (drawing.PointLabels != null)
            {
                problems["pointLabels"] = new[] { "Only a polyline has labels on its points." };
            }

            if (drawing.LineHidden)
            {
                problems["lineHidden"] = new[] { "Only a polyline can hide its line." };
            }
        }
    }

    private static string? CheckMark(WaveMark? mark)
    {
        if (mark == null)
        {
            return "A wave label needs a degree, a label and a side.";
        }

        if (!Enum.IsDefined(mark.Degree) || !Enum.IsDefined(mark.Side) || !WaveNotation.IsLabel(mark.Label))
        {
            return "Unknown degree, label or side.";
        }

        return null;
    }

    private static string? CheckPointLabels(IReadOnlyList<PointLabel>? labels, int points)
    {
        if (labels == null)
        {
            return null;
        }

        var seen = new HashSet<int>();
        foreach (var label in labels)
        {
            if (label == null || label.Point < 0 || label.Point >= points)
            {
                return "A label refers to a point the polyline does not have.";
            }

            if (!seen.Add(label.Point))
            {
                return "A point has one label at most.";
            }

            var markProblem = CheckMark(label.Mark);
            if (markProblem != null)
            {
                return markProblem;
            }
        }

        return null;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")]
    private static partial Regex ColorPattern();
}
