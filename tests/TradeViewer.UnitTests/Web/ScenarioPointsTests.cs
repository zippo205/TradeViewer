using TradeViewer.Contracts.Scenarios;
using TradeViewer.Web.Scenarios;

namespace TradeViewer.UnitTests.Web;

/// <summary>SC10, SC11, SC12 and proposal 9: the point tools by level and verdict, the handles, removing and changing points.</summary>
public class ScenarioPointsTests
{
    private static readonly DateTimeOffset Day = new DateTimeOffset(2024, 3, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_context_offers_the_1_2_tool_with_the_points_still_missing()
    {
        var empty = Scenario("no-trade");
        var withStart = empty with { Waves = new ScenarioWavesRecord("1d", Point(-30), null, null) };

        Assert.Equal(new[] { "waves.wave1Start", "waves.wave1End", "waves.wave2End" }, Fields(ScenarioPoints.ToolsOf("context", empty), "oneTwo"));
        Assert.Equal(new[] { "waves.wave1End", "waves.wave2End" }, Fields(ScenarioPoints.ToolsOf("context", withStart), "oneTwo"));
    }

    [Fact]
    public void A_complete_group_is_placed_again_from_its_first_point()
    {
        var full = Scenario("no-trade") with { Waves = new ScenarioWavesRecord("1d", Point(-30), Point(-10), Point(0)) };

        Assert.Equal(3, Fields(ScenarioPoints.ToolsOf("context", full), "oneTwo").Count);
    }

    [Fact]
    public void Without_the_1_2_the_nested_tools_place_their_own_start_with_it_the_start_is_derived()
    {
        var empty = Scenario("no-trade");
        var withWaves = empty with { Waves = new ScenarioWavesRecord("1d", Point(-30), Point(-10), null) };

        Assert.Equal("wave1.start", Fields(ScenarioPoints.ToolsOf("setup", empty), "wave1Five")[0]);
        Assert.Equal("wave2.start", Fields(ScenarioPoints.ToolsOf("setup", empty), "wave2")[0]);
        Assert.Equal("wave1.iEnd", Fields(ScenarioPoints.ToolsOf("setup", withWaves), "wave1Five")[0]);
        Assert.Equal("wave2.aEnd", Fields(ScenarioPoints.ToolsOf("setup", withWaves), "wave2")[0]);
    }

    [Fact]
    public void A_form_clicked_on_the_chart_is_removed_as_a_whole_and_its_start_like_a_point()
    {
        var scenario = Scenario("trade-win") with
        {
            Waves = new ScenarioWavesRecord("1d", Point(-30), Point(-10), Point(0)),
            Wave1 = new ScenarioWave1Record("4h", "impulse", Point(-28), Start: Point(-30))
        };

        Assert.Null(ScenarioPoints.RemoveGroup(scenario, "wave1").Wave1);
        Assert.NotNull(ScenarioPoints.RemoveGroup(scenario, "wave1").Waves);
        Assert.Null(ScenarioPoints.RemoveGroup(scenario, "waves").Waves);
        Assert.Null(ScenarioPoints.Remove(scenario, "wave1.start").Wave1!.Start);
        Assert.Contains(ScenarioPoints.EntriesOf(scenario), entry => entry.Key == "wave1.start" && entry.Level == "setup");
    }

    [Fact]
    public void The_setup_offers_only_the_wave_1_tool_of_its_pattern()
    {
        var none = Scenario("no-trade");
        var three = none with { Wave1 = new ScenarioWave1Record("4h", "three") };

        Assert.Equal(new[] { "wave1Five", "wave1Three", "wave2" }, ScenarioPoints.ToolsOf("setup", none).Select(tool => tool.Tool.Code));
        Assert.Equal(new[] { "wave1Three", "wave2" }, ScenarioPoints.ToolsOf("setup", three).Select(tool => tool.Tool.Code));
    }

    [Theory]
    [InlineData("trade-win", 6)]
    [InlineData("trade-loss", 6)]
    [InlineData("no-trade", 0)]
    [InlineData(null, 0)]
    public void The_trigger_offers_the_trade_tools_for_a_trade_only(string? verdict, int count)
    {
        var tools = ScenarioPoints.ToolsOf("trigger", Scenario(verdict));

        Assert.Equal(count, tools.Count);
        if (count > 0)
        {
            Assert.Equal("stop", tools.Single(tool => tool.Tool.Code == "exitStop").Tool.Kind);
        }
    }

    [Fact]
    public void Only_the_points_of_the_level_shown_are_editable_and_the_stop_sits_at_the_entry()
    {
        var scenario = Scenario("trade-win") with
        {
            Waves = new ScenarioWavesRecord("1d", Point(-30), Point(-10), Point(0)),
            Trade = new ScenarioTradeRecord("1h", Point(1), new ScenarioPriceRecord(90), null, null, new[] { new ScenarioExitRecord(Day.AddDays(5), 150, 100, "target") })
        };

        var handles = ScenarioPoints.HandlesOf(scenario, "trigger", editable: true);

        Assert.All(handles.Where(handle => handle.Field.StartsWith("waves.", StringComparison.Ordinal)), handle => Assert.False(handle.Editable));
        Assert.All(handles.Where(handle => handle.Field.StartsWith("trade.", StringComparison.Ordinal)), handle => Assert.True(handle.Editable));
        var stop = handles.Single(handle => handle.Field == "trade.stop");
        Assert.Equal((Day.AddDays(1).ToUnixTimeSeconds(), 90.0), (stop.Time, stop.Price));
        Assert.Equal(0, handles.Single(handle => handle.Field == "trade.exits").Index);
        Assert.DoesNotContain(ScenarioPoints.HandlesOf(scenario, "trigger", editable: false), handle => handle.Editable);
    }

    [Fact]
    public void Delete_removes_the_point_of_the_key_and_an_item_of_a_list_by_its_position()
    {
        var scenario = Scenario("trade-win") with
        {
            Waves = new ScenarioWavesRecord("1d", Point(-30), Point(-10), Point(0)),
            Trade = new ScenarioTradeRecord("1h", Point(1), null, null, null, new[]
            {
                new ScenarioExitRecord(Day.AddDays(5), 150, 50, "target"),
                new ScenarioExitRecord(Day.AddDays(6), 160, 50, "target")
            })
        };

        var withoutEnd = ScenarioPoints.Remove(scenario, "waves.wave1End");
        var withoutExit = ScenarioPoints.Remove(scenario, "trade.exits#0");

        Assert.Null(withoutEnd.Waves!.Wave1End);
        Assert.NotNull(withoutEnd.Waves.Wave2End);
        Assert.Equal(160, Assert.Single(withoutExit.Trade!.Exits!).Price);
        Assert.Same(scenario, ScenarioPoints.Remove(scenario, "trade.exits#7"));
    }

    [Fact]
    public void A_reason_is_set_on_its_point_and_an_empty_one_is_removed()
    {
        var scenario = Scenario("no-trade") with { Waves = new ScenarioWavesRecord("1d", Point(-30), null, null) };

        var with = ScenarioPoints.WithWhy(scenario, "waves.wave1Start", "  the low of the year ");
        var without = ScenarioPoints.WithWhy(with, "waves.wave1Start", " ");

        Assert.Equal("the low of the year", with.Waves!.Wave1Start!.Why);
        Assert.Null(without.Waves!.Wave1Start!.Why);
    }

    [Fact]
    public void The_other_pattern_of_wave_1_removes_the_points_of_the_first()
    {
        var five = Scenario("no-trade") with { Wave1 = new ScenarioWave1Record("4h", "impulse", Point(-28), Point(-26), Point(-20)) };

        Assert.Equal(3, ScenarioPoints.Wave1PointsDroppedBy(five, "three"));
        Assert.Equal(0, ScenarioPoints.Wave1PointsDroppedBy(five, "leading-diagonal"));
        var three = ScenarioPoints.WithWave1Pattern(five, "three", "4h");
        Assert.Equal("three", three.Wave1!.Pattern);
        Assert.Null(three.Wave1.IEnd);
        Assert.Null(three.Wave1.IiiEnd);
    }

    [Fact]
    public void A_pattern_chosen_before_a_point_makes_the_section_with_the_setup_timeframe()
    {
        var scenario = ScenarioPoints.WithWave2Pattern(Scenario("no-trade"), "flat", "4h");

        Assert.Equal(("4h", "flat"), (scenario.Wave2!.Timeframe, scenario.Wave2.Pattern));
    }

    [Theory]
    [InlineData("waves.wave1Start", "context")]
    [InlineData("wave1.iiiEnd", "setup")]
    [InlineData("wave2.bEnd", "setup")]
    [InlineData("trade.exits", "trigger")]
    public void Each_field_belongs_to_its_level(string field, string level)
    {
        Assert.Equal(level, ScenarioPoints.LevelOf(field));
    }

    [Fact]
    public void The_problem_field_of_an_item_uses_brackets()
    {
        Assert.Equal("trade.exits[1]", ScenarioPoints.ProblemFieldOf("trade.exits#1"));
        Assert.Equal("trade.entry", ScenarioPoints.ProblemFieldOf("trade.entry"));
    }

    private static IReadOnlyList<string> Fields(IReadOnlyList<ScenarioPointTool> tools, string code)
    {
        return tools.Single(tool => tool.Tool.Code == code).Tool.Fields;
    }

    private static ScenarioPointRecord Point(int days) => new ScenarioPointRecord(Day.AddDays(days), 100 + days);

    private static ExpertScenarioRecord Scenario(string? verdict)
    {
        return new ExpertScenarioRecord(null, "binance", "BTCUSDT", "1d", "long", verdict, true, null, null, Day.AddDays(-60), Day.AddDays(30), null, null, null, null, null, null, null);
    }
}
