namespace TradeViewer.Domain.Drawings;

/// <summary>
/// The rules of each tool (DR1, plan of Drawing Tools, "The tools"):
/// <code>
/// Trend line, arrow      2 anchors
/// Ray                    2 anchors, continues beyond the second
/// Horizontal line        1 anchor (its price)
/// Vertical line          1 anchor (its time)
/// Rectangle              2 anchors (opposite corners), may be filled
/// Parallel channel       3 anchors: the line, then the distance of the parallel; may be filled
/// Text note              1 anchor, needs a text
/// Polyline               2 to MaxPolylinePoints points, wave labels on its points
/// Fibonacci retracement  2 anchors and levels
/// Fibonacci extension    3 anchors (start, end, retracement) and levels
/// Wave label             1 anchor without a price, degree, label and side
/// </code>
/// </summary>
public static class DrawingTools
{
    public const int MaxPolylinePoints = 200;

    public const int MaxTextLength = 2000;

    /// <summary>Lines are blue as the overlays of EWTS (OV6).</summary>
    public const string LineColor = "#2962FF";

    /// <summary>Fibonacci extensions are light blue (DR4).</summary>
    public const string ExtensionColor = "#64B5F6";

    public const string RetracementColor = "#787B86";

    /// <summary>The pale fill of rectangles and channels: the line colour at about 10 % opacity.</summary>
    public const string AreaFill = "#2962FF1A";

    public static IReadOnlyList<DrawingTool> All { get; } = (DrawingTool[])Enum.GetValues(typeof(DrawingTool));

    /// <summary>The payload version the tool writes now. A record of a higher version comes from a newer build and is refused, not guessed at.</summary>
    public static int PayloadVersion(DrawingTool tool)
    {
        return 1;
    }

    /// <summary>The fewest and the most anchors of a drawing of the tool.</summary>
    public static (int Min, int Max) AnchorCount(DrawingTool tool)
    {
        switch (tool)
        {
            case DrawingTool.HorizontalLine:
            case DrawingTool.VerticalLine:
            case DrawingTool.TextNote:
            case DrawingTool.WaveLabel:
                return (1, 1);
            case DrawingTool.TrendLine:
            case DrawingTool.Ray:
            case DrawingTool.Rectangle:
            case DrawingTool.Arrow:
            case DrawingTool.FibonacciRetracement:
                return (2, 2);
            case DrawingTool.ParallelChannel:
            case DrawingTool.FibonacciExtension:
                return (3, 3);
            case DrawingTool.Polyline:
                return (2, MaxPolylinePoints);
            default:
                throw new ArgumentOutOfRangeException(nameof(tool));
        }
    }

    /// <summary>True for the tools that draw an area and may fill it.</summary>
    public static bool HasArea(DrawingTool tool)
    {
        return tool == DrawingTool.Rectangle || tool == DrawingTool.ParallelChannel;
    }

    public static bool IsFibonacci(DrawingTool tool)
    {
        return tool == DrawingTool.FibonacciRetracement || tool == DrawingTool.FibonacciExtension;
    }

    /// <summary>The style a new drawing of the tool starts with.</summary>
    public static DrawingStyle DefaultStyle(DrawingTool tool)
    {
        switch (tool)
        {
            case DrawingTool.FibonacciRetracement:
                return new DrawingStyle(RetracementColor, 1, LineStyle.Solid);
            case DrawingTool.FibonacciExtension:
                return new DrawingStyle(ExtensionColor, 1, LineStyle.Solid);
            case DrawingTool.Rectangle:
            case DrawingTool.ParallelChannel:
                return new DrawingStyle(LineColor, 1, LineStyle.Solid, AreaFill);
            default:
                return new DrawingStyle(LineColor, 2, LineStyle.Solid);
        }
    }
}
