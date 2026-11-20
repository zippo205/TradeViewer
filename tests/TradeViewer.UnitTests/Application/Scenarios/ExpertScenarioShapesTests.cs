using TradeViewer.Application.Features.Scenarios.Services;
using TradeViewer.Contracts.Overlays;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.UnitTests.Application.Scenarios;

/// <summary>SC8, SC21: the picture of an expert scenario, built from its named points in four groups on their levels.</summary>
public class ExpertScenarioShapesTests
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2024, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_internals_of_wave_2_run_from_the_end_of_wave_1_and_without_c_end_at_the_end_of_wave_2()
    {
        var scenario = Scenario() with { Wave2 = new ScenarioWave2Record("4h", "zigzag", AEnd: Point(3, 1.3), BEnd: Point(4, 1.5)) };

        var wave2 = Group(scenario, "wave2");

        Assert.Equal("setup", wave2.Level);
        Assert.Equal(new[] { 1.6, 1.3, 1.5, 1.05 }, Line(wave2).Points!.Select(point => point.Price));
        Assert.Equal(new[] { ("a", "below"), ("b", "above") }, Labels(wave2));
    }

    [Fact]
    public void The_internals_of_wave_1_are_labelled_i_to_v_or_a_to_c()
    {
        var five = Scenario() with { Wave1 = new ScenarioWave1Record("4h", "impulse", IEnd: Point(0.2, 1.2), IiEnd: Point(0.4, 1.1), IiiEnd: Point(1, 1.4), IvEnd: Point(1.5, 1.3), VEnd: Point(2, 1.6)) };
        var three = Scenario() with { Wave1 = new ScenarioWave1Record("4h", "three", AEnd: Point(0.5, 1.3), BEnd: Point(1, 1.2)) };

        Assert.Equal(new[] { "i", "ii", "iii", "iv", "v" }, Labels(Group(five, "wave1")).Select(label => label.Text));
        Assert.Equal(6, Line(Group(five, "wave1")).Points!.Count); // from the start of wave 1, v is the end
        Assert.Equal(new[] { ("a", "above"), ("b", "below") }, Labels(Group(three, "wave1")));
        Assert.Equal(new[] { 1.0, 1.3, 1.2, 1.6 }, Line(Group(three, "wave1")).Points!.Select(point => point.Price)); // without c: to the end of wave 1
    }

    [Fact]
    public void The_stop_steps_at_each_move_until_the_last_exit()
    {
        var trade = new ScenarioTradeRecord(
            "1h", Point(6, 1.1, "EM4"), new ScenarioPriceRecord(0.95, "below wave 2"), null,
            new[] { Point(8, 1.05) },
            new[] { new ScenarioExitRecord(Start.AddDays(10), 1.5, 90, "target"), new ScenarioExitRecord(Start.AddDays(12), 1.05, 10, "stop") });

        var group = Group(Scenario() with { Verdict = "trade-win", Trade = trade }, "trade");

        var stops = group.Shapes.Where(shape => shape.Type == "level").ToList();
        Assert.Equal(new[] { (0.95, Start.AddDays(6), Start.AddDays(8)), (1.05, Start.AddDays(8), Start.AddDays(12)) }, stops.Select(stop => (stop.Price!.Value, stop.From!.Value, stop.To!.Value)));
        Assert.Equal(new[] { "target", "stop" }, group.Shapes.Where(shape => shape.Text!.StartsWith("Exit", StringComparison.Ordinal)).Select(shape => shape.Role));
        Assert.Equal("Entry: 1.1 at 2024-10-07 00:00 UTC - EM4", group.Shapes.Single(shape => shape.Text == "Entry").Detail);
        Assert.Equal("trigger", group.Level);
    }

    [Fact]
    public void A_section_whose_timeframe_is_another_level_is_drawn_on_that_level()
    {
        var scenario = Scenario() with { Wave2 = new ScenarioWave2Record("1h", "zigzag", AEnd: Point(3, 1.3)) };

        Assert.Equal("trigger", Group(scenario, "wave2").Level);
        Assert.Equal("context", Group(scenario with { Wave2 = scenario.Wave2! with { Timeframe = "15m" } }, "wave2").Level);
    }

    [Fact]
    public void A_scenario_without_points_has_no_shapes()
    {
        var empty = Scenario() with { Waves = null };

        Assert.Empty(ExpertScenarioShapes.Of(empty));
        Assert.DoesNotContain(Group(empty with { Waves = new ScenarioWavesRecord("1d", null, null, Point(5, 1)) }, "waves").Shapes, shape => shape.Type == "line");
    }

    private static ExpertScenarioRecord Scenario()
    {
        var waves = new ScenarioWavesRecord("1d", Point(0, 1.0), Point(2, 1.6), Point(5, 1.05));
        return new ExpertScenarioRecord("btcusdt-1d-2024-10-X1", "binance", "BTCUSDT", "1d", "long", "no-trade", true, "why", null, Start, Start.AddDays(30), null, null, waves, null, null, null, null);
    }

    private static ScenarioPointRecord Point(double days, double price, string? why = null)
    {
        return new ScenarioPointRecord(Start.AddDays(days), price, why);
    }

    private static OverlayGroupRecord Group(ExpertScenarioRecord scenario, string key)
    {
        return ExpertScenarioShapes.Of(scenario).Single(group => group.Key == key);
    }

    private static OverlayShapeRecord Line(OverlayGroupRecord group)
    {
        return group.Shapes.Single(shape => shape.Type == "line");
    }

    private static IReadOnlyList<(string Text, string Side)> Labels(OverlayGroupRecord group)
    {
        return group.Shapes.Where(shape => shape.Type == "label").Select(shape => (shape.Text!, shape.Side!)).ToList();
    }
}
