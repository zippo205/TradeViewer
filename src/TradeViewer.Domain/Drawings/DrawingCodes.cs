namespace TradeViewer.Domain.Drawings;

/// <summary>The codes of tools, line styles, wave degrees, sides and Fibonacci kinds as the API and the stored records write them.</summary>
public static class DrawingCodes
{
    private static readonly (DrawingTool Value, string Code)[] Tools =
    {
        (DrawingTool.TrendLine, "trendLine"),
        (DrawingTool.Ray, "ray"),
        (DrawingTool.HorizontalLine, "horizontalLine"),
        (DrawingTool.VerticalLine, "verticalLine"),
        (DrawingTool.ParallelChannel, "parallelChannel"),
        (DrawingTool.Rectangle, "rectangle"),
        (DrawingTool.Arrow, "arrow"),
        (DrawingTool.TextNote, "textNote"),
        (DrawingTool.Polyline, "polyline"),
        (DrawingTool.FibonacciRetracement, "fibonacciRetracement"),
        (DrawingTool.FibonacciExtension, "fibonacciExtension"),
        (DrawingTool.WaveLabel, "waveLabel")
    };

    private static readonly (LineStyle Value, string Code)[] LineStyles =
    {
        (LineStyle.Solid, "solid"),
        (LineStyle.Dashed, "dashed"),
        (LineStyle.Dotted, "dotted")
    };

    private static readonly (WaveDegree Value, string Code)[] Degrees =
    {
        (WaveDegree.GrandSupercycle, "grandSupercycle"),
        (WaveDegree.Supercycle, "supercycle"),
        (WaveDegree.Cycle, "cycle"),
        (WaveDegree.Primary, "primary"),
        (WaveDegree.Intermediate, "intermediate"),
        (WaveDegree.Minor, "minor"),
        (WaveDegree.Minute, "minute"),
        (WaveDegree.Minuette, "minuette"),
        (WaveDegree.Subminuette, "subminuette")
    };

    private static readonly (WaveSide Value, string Code)[] Sides =
    {
        (WaveSide.Above, "above"),
        (WaveSide.Below, "below")
    };

    private static readonly (FibonacciKind Value, string Code)[] Kinds =
    {
        (FibonacciKind.Retracement, "retracement"),
        (FibonacciKind.Extension, "extension")
    };

    public static string Code(DrawingTool tool) => CodeOf(Tools, tool);

    public static string Code(LineStyle style) => CodeOf(LineStyles, style);

    public static string Code(WaveDegree degree) => CodeOf(Degrees, degree);

    public static string Code(WaveSide side) => CodeOf(Sides, side);

    public static string Code(FibonacciKind kind) => CodeOf(Kinds, kind);

    public static bool TryParse(string? code, out DrawingTool tool) => TryParseOf(Tools, code, out tool);

    public static bool TryParse(string? code, out LineStyle style) => TryParseOf(LineStyles, code, out style);

    public static bool TryParse(string? code, out WaveDegree degree) => TryParseOf(Degrees, code, out degree);

    public static bool TryParse(string? code, out WaveSide side) => TryParseOf(Sides, code, out side);

    public static bool TryParse(string? code, out FibonacciKind kind) => TryParseOf(Kinds, code, out kind);

    private static string CodeOf<T>((T Value, string Code)[] table, T value)
        where T : struct, Enum
    {
        foreach (var pair in table)
        {
            if (EqualityComparer<T>.Default.Equals(pair.Value, value))
            {
                return pair.Code;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static bool TryParseOf<T>((T Value, string Code)[] table, string? code, out T value)
        where T : struct, Enum
    {
        foreach (var pair in table)
        {
            if (pair.Code == code)
            {
                value = pair.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
