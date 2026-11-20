using TradeViewer.Domain.MarketData;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.UnitTests.Domain.Scenarios;

/// <summary>SC15: the form checks show each problem at its field; whether the reading follows the strategy is not checked.</summary>
public class ScenarioFormRulesTests
{
    private const long Day = 86_400;
    private const long Hour = 3600;
    private const long Start = 1_727_740_800; // 2024-10-01 00:00 UTC

    [Fact]
    public void A_complete_trade_has_no_problem()
    {
        Assert.Empty(ScenarioFormRules.Check(Trade()));
    }

    [Fact]
    public void A_complete_no_trade_has_no_problem()
    {
        Assert.Empty(ScenarioFormRules.Check(NoTrade()));
    }

    [Fact]
    public void A_no_trade_needs_its_reason_and_has_no_trade()
    {
        var problems = ScenarioFormRules.Check(NoTrade() with { Reason = " ", HasTrade = true });

        Assert.Equal(new[] { "reason", "trade" }, Fields(problems));
    }

    [Fact]
    public void A_trade_needs_entry_stop_and_exits()
    {
        var points = Points(Waves());
        var problems = ScenarioFormRules.Check(Trade() with { Points = points, StopPrice = null, Exits = Array.Empty<ScenarioExit>(), StopMoves = Array.Empty<ScenarioPoint>() });

        Assert.Equal(new[] { "trade.entry", "trade.stop", "trade.exits" }, Fields(problems));
        Assert.Equal(new[] { "trade" }, Fields(ScenarioFormRules.Check(Trade() with { HasTrade = false, Points = points, StopPrice = null, Exits = Array.Empty<ScenarioExit>(), StopMoves = Array.Empty<ScenarioPoint>() })));
    }

    [Fact]
    public void The_head_needs_long_a_context_a_verdict_and_a_span()
    {
        var problems = ScenarioFormRules.Check(NoTrade() with { Direction = "short", ContextTimeframe = Timeframe.H1, Verdict = null, From = null, To = null });

        Assert.Equal(new[] { "direction", "contextTimeframe", "verdict", "from", "to" }, Fields(problems));
        Assert.Equal(new[] { "verdict" }, Fields(ScenarioFormRules.Check(NoTrade() with { Verdict = "won" })));
        Assert.Equal(new[] { "to" }, Fields(ScenarioFormRules.Check(NoTrade() with { To = Start })));
    }

    [Fact]
    public void Every_wave_point_is_required_and_they_follow_one_another()
    {
        var missing = Points(Waves()).Where(point => point.Key != ScenarioPointName.Wave1End).ToDictionary(point => point.Key, point => point.Value);
        Assert.Equal(new[] { "waves.wave1End" }, Fields(ScenarioFormRules.Check(NoTrade() with { Points = missing })));

        var swapped = Points(Waves());
        swapped[ScenarioPointName.Wave2End] = new ScenarioPoint(Start + Day, 0.9); // before the end of wave 1
        Assert.Equal(new[] { "waves.wave2End" }, Fields(ScenarioFormRules.Check(NoTrade() with { Points = swapped })));
    }

    [Fact]
    public void The_shares_of_the_exits_add_up_to_100()
    {
        var exits = new[] { Exit(12, 50, ScenarioCodes.Target), Exit(13, 40, ScenarioCodes.Target) };

        var problem = Assert.Single(ScenarioFormRules.Check(Trade() with { Exits = exits }));

        Assert.Equal("trade.exits", problem.Field);
        Assert.Contains("90 %", problem.Message);
    }

    [Fact]
    public void A_share_and_a_kind_are_checked_at_their_exit()
    {
        var exits = new[] { Exit(12, 100, ScenarioCodes.Target), Exit(13, 0, "sold") };

        Assert.Equal(new[] { "trade.exits[1].share", "trade.exits[1].kind" }, Fields(ScenarioFormRules.Check(Trade() with { Exits = exits })));
    }

    [Fact]
    public void The_trade_follows_the_end_of_wave_2_and_its_points_are_in_time_order()
    {
        var points = Points(Waves());
        points[ScenarioPointName.Entry] = new ScenarioPoint(Start + (5 * Day), 1.1); // the candle of the end of wave 2
        points[ScenarioPointName.Add] = new ScenarioPoint(Start + (5 * Day), 1.2);
        var stopMoves = new[] { new ScenarioPoint(Start + (9 * Day), 1.0), new ScenarioPoint(Start + (8 * Day), 1.05) };
        var exits = new[] { Exit(12, 50, ScenarioCodes.Target), Exit(11, 50, ScenarioCodes.StopKind) };

        var problems = ScenarioFormRules.Check(Trade() with { Points = points, StopMoves = stopMoves, Exits = exits });

        Assert.Equal(new[] { "trade.entry", "trade.add", "trade.stopMoves[1]", "trade.exits[1]" }, Fields(problems));
    }

    [Fact]
    public void Every_point_lies_inside_the_time_span()
    {
        var problems = ScenarioFormRules.Check(Trade() with { From = Start + Day, To = Start + (12 * Day) });

        Assert.Equal(new[] { "waves.wave1Start", "trade.exits[1]" }, Fields(problems));
    }

    [Fact]
    public void Prices_are_numbers_above_zero()
    {
        var points = Points(Waves());
        points[ScenarioPointName.Wave1End] = new ScenarioPoint(Start + (2 * Day), double.NaN);

        var problems = ScenarioFormRules.Check(Trade() with { Points = points, StopPrice = 0 });

        Assert.Contains(problems, problem => problem.Field == "waves.wave1End");
        Assert.Contains(problems, problem => problem.Field == "trade.stop");
    }

    [Fact]
    public void A_zigzag_and_the_flats_need_a_and_b_of_wave_2()
    {
        var points = Points(Waves());

        Assert.Equal(new[] { "wave2.aEnd", "wave2.bEnd" }, Fields(ScenarioFormRules.Check(NoTrade() with { Points = points, HasWave2 = true, Wave2Pattern = ScenarioCodes.RunningFlat })));
        Assert.Empty(ScenarioFormRules.Check(NoTrade() with { Points = points, HasWave2 = true, Wave2Pattern = ScenarioCodes.Triangle }));
        Assert.Equal(new[] { "wave2.pattern" }, Fields(ScenarioFormRules.Check(NoTrade() with { Points = points, HasWave2 = true, Wave2Pattern = null })));
    }

    [Fact]
    public void The_internals_of_wave_2_lie_between_the_end_of_wave_1_and_the_candle_of_the_end_of_wave_2()
    {
        var points = Points(Waves());
        points[ScenarioPointName.Wave2A] = new ScenarioPoint(Start + (3 * Day), 1.3);
        points[ScenarioPointName.Wave2B] = new ScenarioPoint(Start + (3 * Day) + (8 * Hour), 1.5); // 4h candles inside the days
        points[ScenarioPointName.Wave2C] = new ScenarioPoint(Start + (5 * Day) + (20 * Hour), 1.0); // the last 4h of the day of wave 2 end
        Assert.Empty(ScenarioFormRules.Check(NoTrade() with { Points = points, HasWave2 = true, Wave2Pattern = ScenarioCodes.Zigzag }));

        points[ScenarioPointName.Wave2C] = new ScenarioPoint(Start + (6 * Day), 1.0);
        points[ScenarioPointName.Wave2A] = new ScenarioPoint(Start + (2 * Day), 1.3); // the open of the day of wave 1 end
        Assert.Equal(new[] { "wave2.aEnd", "wave2.cEnd" }, Fields(ScenarioFormRules.Check(NoTrade() with { Points = points, HasWave2 = true, Wave2Pattern = ScenarioCodes.Zigzag })));
    }

    [Fact]
    public void A_five_needs_i_to_iv_and_v_is_optional()
    {
        var points = Points(Waves());
        foreach (var (name, hour) in new[] { (ScenarioPointName.Wave1I, 4), (ScenarioPointName.Wave1Ii, 12), (ScenarioPointName.Wave1Iii, 28), (ScenarioPointName.Wave1Iv, 36) })
        {
            points[name] = new ScenarioPoint(Start + (hour * Hour), 1.2);
        }

        Assert.Empty(ScenarioFormRules.Check(NoTrade() with { Points = points, HasWave1 = true, Wave1Pattern = ScenarioCodes.Impulse }));

        points.Remove(ScenarioPointName.Wave1Iii);
        Assert.Equal(new[] { "wave1.iiiEnd" }, Fields(ScenarioFormRules.Check(NoTrade() with { Points = points, HasWave1 = true, Wave1Pattern = ScenarioCodes.LeadingDiagonal })));
    }

    [Fact]
    public void A_three_needs_a_and_b_and_never_holds_the_points_of_a_five()
    {
        var points = Points(Waves());
        points[ScenarioPointName.Wave1A] = new ScenarioPoint(Start + (8 * Hour), 1.3);
        points[ScenarioPointName.Wave1I] = new ScenarioPoint(Start + (4 * Hour), 1.2);

        var problems = ScenarioFormRules.Check(NoTrade() with { Points = points, HasWave1 = true, Wave1Pattern = ScenarioCodes.Three });

        Assert.Equal(new[] { "wave1.bEnd", "wave1.iEnd" }, Fields(problems));
        Assert.Contains("EWTS", problems[1].Message);
    }

    [Fact]
    public void The_internals_of_wave_1_lie_after_its_start_and_in_order()
    {
        var points = Points(Waves());
        points[ScenarioPointName.Wave1A] = new ScenarioPoint(Start, 1.3);
        points[ScenarioPointName.Wave1B] = new ScenarioPoint(Start + (12 * Hour), 1.1);
        points[ScenarioPointName.Wave1C] = new ScenarioPoint(Start + (8 * Hour), 1.4);

        var problems = ScenarioFormRules.Check(NoTrade() with { Points = points, HasWave1 = true, Wave1Pattern = ScenarioCodes.Three });

        Assert.Equal(new[] { "wave1.aEnd", "wave1.cEnd" }, Fields(problems));
    }

    [Fact]
    public void Several_problems_are_all_reported_none_prevents_anything()
    {
        var empty = new ExpertScenarioReading(Timeframe.D1, ScenarioCodes.Long, null, null, null, null, new Dictionary<ScenarioPointName, ScenarioPoint>(), false, null, false, null, false, null, Array.Empty<ScenarioPoint>(), Array.Empty<ScenarioExit>());

        Assert.Equal(new[] { "verdict", "from", "to", "waves.wave1Start", "waves.wave1End", "waves.wave2End" }, Fields(ScenarioFormRules.Check(empty)));
    }

    private static ExpertScenarioReading NoTrade()
    {
        return new ExpertScenarioReading(
            Timeframe.D1, ScenarioCodes.Long, ScenarioCodes.NoTrade, "wave 2 too deep", Start, Start + (30 * Day), Points(Waves()),
            false, null, false, null, false, null, Array.Empty<ScenarioPoint>(), Array.Empty<ScenarioExit>());
    }

    private static ExpertScenarioReading Trade()
    {
        var points = Points(Waves());
        points[ScenarioPointName.Entry] = new ScenarioPoint(Start + (6 * Day) + (2 * Hour), 1.1);
        points[ScenarioPointName.Add] = new ScenarioPoint(Start + (6 * Day) + (3 * Hour), 1.2);
        return new ExpertScenarioReading(
            Timeframe.D1, ScenarioCodes.Long, ScenarioCodes.TradeWin, null, Start, Start + (30 * Day), points,
            false, null, false, null, true, 0.95,
            new[] { new ScenarioPoint(Start + (8 * Day), 1.05) },
            new[] { Exit(12, 90, ScenarioCodes.Target), Exit(14, 10, ScenarioCodes.StopKind) });
    }

    private static (ScenarioPointName Name, ScenarioPoint Point)[] Waves()
    {
        return new[]
        {
            (ScenarioPointName.Wave1Start, new ScenarioPoint(Start, 1.0)),
            (ScenarioPointName.Wave1End, new ScenarioPoint(Start + (2 * Day), 1.6)),
            (ScenarioPointName.Wave2End, new ScenarioPoint(Start + (5 * Day), 1.05))
        };
    }

    private static Dictionary<ScenarioPointName, ScenarioPoint> Points((ScenarioPointName Name, ScenarioPoint Point)[] points)
    {
        return points.ToDictionary(point => point.Name, point => point.Point);
    }

    private static ScenarioExit Exit(int day, double share, string kind)
    {
        return new ScenarioExit(Start + (day * Day), 1.5, share, kind);
    }

    private static IReadOnlyList<string> Fields(IReadOnlyList<ScenarioFormProblem> problems)
    {
        return problems.Select(problem => problem.Field).ToList();
    }
}
