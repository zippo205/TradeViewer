using TradeViewer.Domain.Drawings;

namespace TradeViewer.UnitTests.Domain.Drawings;

/// <summary>The codes of the drawing records: every value has one, and every code reads back to its value.</summary>
public class DrawingCodesTests
{
    [Fact]
    public void Every_tool_has_a_code_that_reads_back()
    {
        foreach (var tool in DrawingTools.All)
        {
            Assert.True(DrawingCodes.TryParse(DrawingCodes.Code(tool), out DrawingTool parsed));
            Assert.Equal(tool, parsed);
        }

        Assert.Equal(12, DrawingTools.All.Count); // DR1
        Assert.Equal("fibonacciRetracement", DrawingCodes.Code(DrawingTool.FibonacciRetracement));
    }

    [Fact]
    public void Every_degree_side_line_style_and_kind_has_a_code_that_reads_back()
    {
        foreach (var degree in WaveNotation.Degrees)
        {
            Assert.True(DrawingCodes.TryParse(DrawingCodes.Code(degree), out WaveDegree parsed));
            Assert.Equal(degree, parsed);
        }

        foreach (var side in Enum.GetValues<WaveSide>())
        {
            Assert.True(DrawingCodes.TryParse(DrawingCodes.Code(side), out WaveSide parsed));
            Assert.Equal(side, parsed);
        }

        foreach (var style in Enum.GetValues<LineStyle>())
        {
            Assert.True(DrawingCodes.TryParse(DrawingCodes.Code(style), out LineStyle parsed));
            Assert.Equal(style, parsed);
        }

        foreach (var kind in Enum.GetValues<FibonacciKind>())
        {
            Assert.True(DrawingCodes.TryParse(DrawingCodes.Code(kind), out FibonacciKind parsed));
            Assert.Equal(kind, parsed);
        }
    }

    [Theory]
    [InlineData("TrendLine")]
    [InlineData("trend-line")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_code_is_not_read(string? code)
    {
        Assert.False(DrawingCodes.TryParse(code, out DrawingTool _));
    }

    [Fact]
    public void Every_tool_has_an_anchor_rule_and_a_payload_version()
    {
        foreach (var tool in DrawingTools.All)
        {
            var (min, max) = DrawingTools.AnchorCount(tool);
            Assert.InRange(min, 1, max);
            Assert.True(DrawingTools.PayloadVersion(tool) >= 1);
        }
    }
}
