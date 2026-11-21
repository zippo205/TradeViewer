using TradeViewer.Contracts.Drawings;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Web.Drawings;
using TradeViewer.Web.Scenarios;

namespace TradeViewer.UnitTests.Web;

/// <summary>SC20, decision 0012: the free drawings of a scenario, each with its level.</summary>
public class ScenarioDrawingsTests
{
    private static readonly DateTimeOffset Day = new DateTimeOffset(2024, 3, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_drawing_of_another_level_reaches_the_chart_locked_and_keeps_its_own_lock()
    {
        var drawing = Drawing(Guid.NewGuid(), "context");

        Assert.True(ScenarioDrawings.ToChart(drawing, editable: false).Locked);
        Assert.False(ScenarioDrawings.ToChart(drawing, editable: true).Locked);
        Assert.True(ScenarioDrawings.ToChart(drawing with { Locked = true }, editable: true).Locked);
    }

    [Fact]
    public void A_drawing_of_the_chart_becomes_a_record_of_the_level_and_goes_back_unchanged()
    {
        var drawing = Drawing(Guid.NewGuid(), "setup");

        var back = ScenarioDrawings.ToRecord(ScenarioDrawings.ToChart(drawing, editable: true), "setup")!;

        Assert.Equal(drawing.Id, back.Id);
        Assert.Equal(("setup", "trendLine", Day, 100.0), (back.Level, back.Tool, back.Anchors[0].Time, back.Anchors[0].Price!.Value));
        Assert.Null(ScenarioDrawings.ToRecord(new ChartDrawing("not a uuid", "trendLine", false, Array.Empty<ChartAnchor>(), drawing.Style), "setup"));
    }

    [Fact]
    public void Adding_replaces_a_drawing_with_the_same_id_and_removing_the_last_one_leaves_no_section()
    {
        var id = Guid.NewGuid();
        var scenario = new ExpertScenarioRecord(null, "binance", "BTCUSDT", "1d", "long", null, true, null, null, Day, Day.AddDays(30), null, null, null, null, null, null, null);

        scenario = ScenarioDrawings.With(scenario, Drawing(id, "context"));
        scenario = ScenarioDrawings.With(scenario, Drawing(id, "context") with { Text = "moved" });

        Assert.Equal("moved", Assert.Single(scenario.Drawings!).Text);
        Assert.Null(ScenarioDrawings.Without(scenario, id).Drawings);
    }

    [Fact]
    public void The_bar_of_the_selected_drawing_edits_it_on_its_own_level()
    {
        var drawing = Drawing(Guid.NewGuid(), "trigger");

        var bar = ScenarioDrawings.ToBar(drawing, "binance", "BTCUSDT");
        var back = ScenarioDrawings.FromBar(bar with { Style = bar.Style with { Color = "#FF0000" } }, drawing.Level);

        Assert.Equal(("trigger", "#FF0000"), (back.Level, back.Style.Color));
    }

    private static ScenarioDrawingRecord Drawing(Guid id, string level)
    {
        return new ScenarioDrawingRecord(id, level, "trendLine", 1, false,
            new[] { new AnchorRecord(Day, 100), new AnchorRecord(Day.AddDays(3), 120) }, new DrawingStyleRecord("#2962FF", 2, "solid"));
    }
}
