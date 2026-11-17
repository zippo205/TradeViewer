using TradeViewer.Domain.Drawings;

namespace TradeViewer.UnitTests.Domain.Drawings;

/// <summary>DR1 to DR5: what a drawing must be to be stored; what cannot be read again is refused.</summary>
public class DrawingRulesTests
{
    private const long Time = 1_763_164_800; // 2025-11-15 00:00 UTC

    [Theory]
    [MemberData(nameof(AllTools))]
    public void A_drawing_as_each_tool_makes_it_is_valid(DrawingTool tool)
    {
        Assert.Empty(DrawingRules.Check(Valid(tool)));
    }

    [Theory]
    [InlineData(DrawingTool.TrendLine, 1)]
    [InlineData(DrawingTool.TrendLine, 3)]
    [InlineData(DrawingTool.HorizontalLine, 2)]
    [InlineData(DrawingTool.ParallelChannel, 2)]
    [InlineData(DrawingTool.FibonacciExtension, 2)]
    [InlineData(DrawingTool.Polyline, 1)]
    [InlineData(DrawingTool.Polyline, DrawingTools.MaxPolylinePoints + 1)]
    public void A_wrong_number_of_anchors_is_refused(DrawingTool tool, int count)
    {
        var drawing = Valid(tool) with { Anchors = Anchors(count, tool == DrawingTool.WaveLabel) };

        Assert.Contains("anchors", DrawingRules.Check(drawing).Keys);
    }

    [Fact]
    public void A_polyline_may_have_up_to_the_limit_of_points()
    {
        var drawing = Valid(DrawingTool.Polyline) with { Anchors = Anchors(DrawingTools.MaxPolylinePoints, false) };

        Assert.Empty(DrawingRules.Check(drawing));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_price_that_is_not_a_finite_number_is_refused(double price)
    {
        var drawing = Valid(DrawingTool.TrendLine) with { Anchors = new[] { new Anchor(Time, 100), new Anchor(Time + 3600, price) } };

        Assert.Contains("anchors", DrawingRules.Check(drawing).Keys);
    }

    [Fact]
    public void An_anchor_without_a_price_is_refused_for_every_tool_but_the_wave_label()
    {
        var line = Valid(DrawingTool.TrendLine) with { Anchors = new[] { new Anchor(Time, 100), new Anchor(Time + 3600, null) } };
        var label = Valid(DrawingTool.WaveLabel) with { Anchors = new[] { new Anchor(Time, 100) } };

        Assert.Contains("anchors", DrawingRules.Check(line).Keys);
        Assert.Contains("anchors", DrawingRules.Check(label).Keys);
    }

    [Fact]
    public void Anchors_after_the_last_candle_are_allowed_but_not_beyond_2200()
    {
        var future = Valid(DrawingTool.TrendLine) with { Anchors = new[] { new Anchor(Time, 100), new Anchor(Time + 400L * 86400, 120) } };
        var tooFar = Valid(DrawingTool.TrendLine) with { Anchors = new[] { new Anchor(Time, 100), new Anchor(DrawingRules.LatestTime + 1, 120) } };
        var beforeUnixTime = Valid(DrawingTool.VerticalLine) with { Anchors = new[] { new Anchor(-1, 100) } };

        Assert.Empty(DrawingRules.Check(future));
        Assert.Contains("anchors", DrawingRules.Check(tooFar).Keys);
        Assert.Contains("anchors", DrawingRules.Check(beforeUnixTime).Keys);
    }

    [Fact]
    public void A_payload_version_this_build_does_not_know_is_refused()
    {
        Assert.Contains("version", DrawingRules.Check(Valid(DrawingTool.Ray) with { Version = 2 }).Keys);
        Assert.Contains("version", DrawingRules.Check(Valid(DrawingTool.Ray) with { Version = 0 }).Keys);
    }

    [Fact]
    public void An_unknown_tool_is_refused()
    {
        var problems = DrawingRules.Check(Valid(DrawingTool.TrendLine) with { Tool = (DrawingTool)99 });

        Assert.Equal(new[] { "tool" }, problems.Keys);
    }

    [Fact]
    public void Id_and_asset_are_required()
    {
        var problems = DrawingRules.Check(Valid(DrawingTool.TrendLine) with { Id = Guid.Empty, Symbol = " " });

        Assert.Contains("id", problems.Keys);
        Assert.Contains("asset", problems.Keys);
    }

    [Theory]
    [InlineData("#2962FF", true)]
    [InlineData("#2962ff80", true)]
    [InlineData("2962FF", false)]
    [InlineData("#2962F", false)]
    [InlineData("#2962FFG0", false)]
    [InlineData("blue", false)]
    [InlineData("", false)]
    public void Colours_are_written_as_hex(string color, bool valid)
    {
        var problems = DrawingRules.Check(Valid(DrawingTool.TrendLine) with { Style = new DrawingStyle(color, 2, LineStyle.Solid) });

        Assert.Equal(valid, !problems.ContainsKey("style.color"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void The_width_is_one_to_four_pixels(int width)
    {
        var problems = DrawingRules.Check(Valid(DrawingTool.TrendLine) with { Style = new DrawingStyle("#2962FF", width, LineStyle.Dashed) });

        Assert.Contains("style.width", problems.Keys);
    }

    [Fact]
    public void Only_rectangles_and_channels_have_a_fill()
    {
        var filledLine = Valid(DrawingTool.TrendLine) with { Style = new DrawingStyle("#2962FF", 2, LineStyle.Solid, "#2962FF1A") };
        var badFill = Valid(DrawingTool.Rectangle) with { Style = new DrawingStyle("#2962FF", 1, LineStyle.Solid, "pale") };
        var noFill = Valid(DrawingTool.Rectangle) with { Style = new DrawingStyle("#2962FF", 1, LineStyle.Solid) };

        Assert.Contains("style.fill", DrawingRules.Check(filledLine).Keys);
        Assert.Contains("style.fill", DrawingRules.Check(badFill).Keys);
        Assert.Empty(DrawingRules.Check(noFill));
    }

    [Fact]
    public void A_text_note_needs_a_text_and_no_text_is_too_long()
    {
        var empty = Valid(DrawingTool.TextNote) with { Text = "  " };
        var tooLong = Valid(DrawingTool.TrendLine) with { Text = new string('x', DrawingTools.MaxTextLength + 1) };
        var lineWithText = Valid(DrawingTool.TrendLine) with { Text = "neckline" };

        Assert.Contains("text", DrawingRules.Check(empty).Keys);
        Assert.Contains("text", DrawingRules.Check(tooLong).Keys);
        Assert.Empty(DrawingRules.Check(lineWithText));
    }

    [Fact]
    public void A_Fibonacci_drawing_needs_its_levels_and_no_other_tool_has_any()
    {
        var withoutLevels = Valid(DrawingTool.FibonacciRetracement) with { Levels = null };
        var twice = Valid(DrawingTool.FibonacciRetracement) with { Levels = new[] { new FibonacciLevel(0.5), new FibonacciLevel(0.5) } };
        var lineWithLevels = Valid(DrawingTool.TrendLine) with { Levels = new[] { new FibonacciLevel(0.5) } };

        Assert.Contains("levels", DrawingRules.Check(withoutLevels).Keys);
        Assert.Contains("levels", DrawingRules.Check(twice).Keys);
        Assert.Contains("levels", DrawingRules.Check(lineWithLevels).Keys);
    }

    [Fact]
    public void A_wave_label_needs_a_known_degree_label_and_side()
    {
        var missing = Valid(DrawingTool.WaveLabel) with { Wave = null };
        var unknownLabel = Valid(DrawingTool.WaveLabel) with { Wave = new WaveMark(WaveDegree.Minor, "6", WaveSide.Above) };
        var unknownDegree = Valid(DrawingTool.WaveLabel) with { Wave = new WaveMark((WaveDegree)42, "1", WaveSide.Above) };
        var lineWithMark = Valid(DrawingTool.TrendLine) with { Wave = new WaveMark(WaveDegree.Minor, "1", WaveSide.Above) };

        Assert.Contains("wave", DrawingRules.Check(missing).Keys);
        Assert.Contains("wave", DrawingRules.Check(unknownLabel).Keys);
        Assert.Contains("wave", DrawingRules.Check(unknownDegree).Keys);
        Assert.Contains("wave", DrawingRules.Check(lineWithMark).Keys);
    }

    [Fact]
    public void Labels_on_a_polyline_refer_to_its_points_one_each()
    {
        var mark = new WaveMark(WaveDegree.Intermediate, "1", WaveSide.Below);
        var outside = Valid(DrawingTool.Polyline) with { PointLabels = new[] { new PointLabel(5, mark) } };
        var twice = Valid(DrawingTool.Polyline) with { PointLabels = new[] { new PointLabel(1, mark), new PointLabel(1, mark) } };
        var hidden = Valid(DrawingTool.Polyline) with { LineHidden = true, PointLabels = new[] { new PointLabel(0, mark), new PointLabel(4, mark with { Label = "5" }) } };

        Assert.Contains("pointLabels", DrawingRules.Check(outside).Keys);
        Assert.Contains("pointLabels", DrawingRules.Check(twice).Keys);
        Assert.Empty(DrawingRules.Check(hidden));
    }

    [Fact]
    public void Only_a_polyline_has_point_labels_or_hides_its_line()
    {
        var mark = new WaveMark(WaveDegree.Minor, "1", WaveSide.Above);
        var problems = DrawingRules.Check(Valid(DrawingTool.TrendLine) with { LineHidden = true, PointLabels = new[] { new PointLabel(0, mark) } });

        Assert.Contains("pointLabels", problems.Keys);
        Assert.Contains("lineHidden", problems.Keys);
    }

    [Fact]
    public void Every_default_style_is_valid_for_its_tool()
    {
        foreach (var tool in DrawingTools.All)
        {
            Assert.Empty(DrawingRules.Check(Valid(tool) with { Style = DrawingTools.DefaultStyle(tool) }));
        }
    }

    public static TheoryData<DrawingTool> AllTools()
    {
        var data = new TheoryData<DrawingTool>();
        foreach (var tool in DrawingTools.All)
        {
            data.Add(tool);
        }

        return data;
    }

    /// <summary>A drawing of the tool as the chart would make it.</summary>
    private static Drawing Valid(DrawingTool tool)
    {
        var (min, _) = DrawingTools.AnchorCount(tool);
        var count = tool == DrawingTool.Polyline ? 5 : min;
        var drawing = new Drawing(
            Guid.NewGuid(),
            "binance",
            "BTCUSDT",
            tool,
            DrawingTools.PayloadVersion(tool),
            false,
            Anchors(count, tool == DrawingTool.WaveLabel),
            DrawingTools.DefaultStyle(tool));

        switch (tool)
        {
            case DrawingTool.TextNote:
                return drawing with { Text = "breakout?" };
            case DrawingTool.FibonacciRetracement:
                return drawing with { Levels = FibonacciPresets.Retracement.Levels };
            case DrawingTool.FibonacciExtension:
                return drawing with { Levels = FibonacciPresets.Extension.Levels };
            case DrawingTool.WaveLabel:
                return drawing with { Wave = new WaveMark(WaveDegree.Primary, "3", WaveSide.Above) };
            default:
                return drawing;
        }
    }

    private static Anchor[] Anchors(int count, bool withoutPrice)
    {
        return Enumerable.Range(0, count)
            .Select(index => new Anchor(Time + index * 3600L, withoutPrice ? null : 90000 + index * 100))
            .ToArray();
    }
}
