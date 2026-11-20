using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Scenarios;

/// <summary>
/// The form checks of the editor (SC15): what the format requires, never whether the reading follows the strategy
/// (EWTS's rule checks). A problem never prevents a save; the editor shows each one at its field.
/// <list type="bullet">
/// <item>the head: long, context 1d or 4h, a known verdict, from before to, a reason for a no-trade;</item>
/// <item>the fields the verdict requires: the three wave points always; entry, stop and an exit for a trade, no
/// trade for a no-trade;</item>
/// <item>the fields the patterns require: i to iv of a five, a and b of a three, never both; a and b of wave 2 for a
/// zigzag and the flats;</item>
/// <item>the order of the times, the shares adding up to 100 %, every point inside from..to, prices above zero.</item>
/// </list>
/// </summary>
public static class ScenarioFormRules
{
    private const double ShareTolerance = 1e-9;

    public static IReadOnlyList<ScenarioFormProblem> Check(ExpertScenarioReading scenario)
    {
        var problems = new List<ScenarioFormProblem>();
        CheckHead(scenario, problems);
        CheckWaves(scenario, problems);
        CheckWave1(scenario, problems);
        CheckWave2(scenario, problems);
        CheckTrade(scenario, problems);
        CheckPrices(scenario, problems);
        CheckSpan(scenario, problems);
        return problems;
    }

    private static void CheckHead(ExpertScenarioReading scenario, List<ScenarioFormProblem> problems)
    {
        if (scenario.Direction != ScenarioCodes.Long)
        {
            problems.Add(new ScenarioFormProblem("direction", "Every expert scenario is long."));
        }

        if (scenario.ContextTimeframe is not { } context || !ScenarioLevels.IsContext(context))
        {
            problems.Add(new ScenarioFormProblem("contextTimeframe", "The context timeframe is 1d or 4h."));
        }

        if (scenario.Verdict == null)
        {
            problems.Add(new ScenarioFormProblem("verdict", "Choose the verdict: trade-win, trade-loss or no-trade."));
        }
        else if (!ScenarioCodes.Verdicts.Contains(scenario.Verdict))
        {
            problems.Add(new ScenarioFormProblem("verdict", "Unknown verdict \"" + scenario.Verdict + "\"."));
        }

        if (scenario.Verdict == ScenarioCodes.NoTrade && string.IsNullOrWhiteSpace(scenario.Reason))
        {
            problems.Add(new ScenarioFormProblem("reason", "A no-trade needs the reason why there is no trade."));
        }

        if (scenario.From == null)
        {
            problems.Add(new ScenarioFormProblem("from", "The time span needs its start."));
        }

        if (scenario.To == null)
        {
            problems.Add(new ScenarioFormProblem("to", "The time span needs its end."));
        }

        if (scenario.From != null && scenario.To != null && scenario.From >= scenario.To)
        {
            problems.Add(new ScenarioFormProblem("to", "The end of the time span is after its start."));
        }
    }

    private static void CheckWaves(ExpertScenarioReading scenario, List<ScenarioFormProblem> problems)
    {
        var waves = new[] { ScenarioPointName.Wave1Start, ScenarioPointName.Wave1End, ScenarioPointName.Wave2End };
        foreach (var name in waves)
        {
            if (!scenario.Points.ContainsKey(name))
            {
                problems.Add(Problem(name, "Place " + Label(name) + "."));
            }
        }

        CheckOrder(scenario, waves, lowerBound: null, upperBound: null, problems);
    }

    private static void CheckWave1(ExpertScenarioReading scenario, List<ScenarioFormProblem> problems)
    {
        var fivePlaced = ScenarioPoints.Wave1Five.Where(scenario.Points.ContainsKey).ToList();
        var threePlaced = ScenarioPoints.Wave1Three.Where(scenario.Points.ContainsKey).ToList();
        if (!scenario.HasWave1 && fivePlaced.Count == 0 && threePlaced.Count == 0)
        {
            return;
        }

        var pattern = scenario.Wave1Pattern;
        IReadOnlyList<ScenarioPointName>? points = null;
        if (pattern == null)
        {
            problems.Add(new ScenarioFormProblem("wave1.pattern", "Choose the pattern of wave 1: impulse, leading-diagonal or three."));
        }
        else if (!ScenarioCodes.Wave1Patterns.Contains(pattern))
        {
            problems.Add(new ScenarioFormProblem("wave1.pattern", "Unknown pattern of wave 1 \"" + pattern + "\"."));
        }
        else if (pattern == ScenarioCodes.Three)
        {
            points = ScenarioPoints.Wave1Three;
            Require(scenario, new[] { ScenarioPointName.Wave1A, ScenarioPointName.Wave1B }, "a three", problems);
            Forbid(fivePlaced, "a three", problems);
        }
        else
        {
            points = ScenarioPoints.Wave1Five;
            Require(scenario, new[] { ScenarioPointName.Wave1I, ScenarioPointName.Wave1Ii, ScenarioPointName.Wave1Iii, ScenarioPointName.Wave1Iv }, "a five", problems);
            Forbid(threePlaced, "a five", problems);
        }

        if (points != null)
        {
            CheckOrder(scenario, points, ScenarioPointName.Wave1Start, ScenarioPointName.Wave1End, problems);
        }
    }

    private static void CheckWave2(ExpertScenarioReading scenario, List<ScenarioFormProblem> problems)
    {
        if (!scenario.HasWave2 && !ScenarioPoints.Wave2Points.Any(scenario.Points.ContainsKey))
        {
            return;
        }

        var pattern = scenario.Wave2Pattern;
        if (pattern == null)
        {
            problems.Add(new ScenarioFormProblem("wave2.pattern", "Choose the pattern of wave 2."));
        }
        else if (!ScenarioCodes.Wave2Patterns.Contains(pattern))
        {
            problems.Add(new ScenarioFormProblem("wave2.pattern", "Unknown pattern of wave 2 \"" + pattern + "\"."));
        }
        else if (ScenarioCodes.Wave2PatternsWithAb.Contains(pattern))
        {
            Require(scenario, new[] { ScenarioPointName.Wave2A, ScenarioPointName.Wave2B }, "a " + pattern, problems);
        }

        CheckOrder(scenario, ScenarioPoints.Wave2Points, ScenarioPointName.Wave1End, ScenarioPointName.Wave2End, problems);
    }

    private static void CheckTrade(ExpertScenarioReading scenario, List<ScenarioFormProblem> problems)
    {
        if (scenario.Verdict == ScenarioCodes.NoTrade)
        {
            if (scenario.HasTrade)
            {
                problems.Add(new ScenarioFormProblem("trade", "A no-trade has no trade; remove it or change the verdict."));
            }

            return;
        }

        var isTrade = ScenarioCodes.IsTrade(scenario.Verdict);
        if (!scenario.HasTrade)
        {
            if (isTrade)
            {
                problems.Add(new ScenarioFormProblem("trade", "A " + scenario.Verdict + " needs its trade: entry, stop and exits."));
            }

            return;
        }

        if (isTrade && !scenario.Points.ContainsKey(ScenarioPointName.Entry))
        {
            problems.Add(Problem(ScenarioPointName.Entry, "Place the entry."));
        }

        if (isTrade && scenario.StopPrice == null)
        {
            problems.Add(new ScenarioFormProblem("trade.stop", "Set the stop."));
        }

        if (isTrade && scenario.Exits.Count == 0)
        {
            problems.Add(new ScenarioFormProblem("trade.exits", "Place the exits."));
        }

        CheckTradeTimes(scenario, problems);
        CheckExits(scenario, problems);
    }

    private static void CheckTradeTimes(ExpertScenarioReading scenario, List<ScenarioFormProblem> problems)
    {
        var hasEntry = scenario.Points.TryGetValue(ScenarioPointName.Entry, out var entry);
        if (hasEntry && scenario.Points.TryGetValue(ScenarioPointName.Wave2End, out var wave2End) && entry.Time <= wave2End.Time)
        {
            problems.Add(Problem(ScenarioPointName.Entry, "The entry is after the end of wave 2."));
        }

        if (!hasEntry)
        {
            return;
        }

        if (scenario.Points.TryGetValue(ScenarioPointName.Add, out var add) && add.Time <= entry.Time)
        {
            problems.Add(Problem(ScenarioPointName.Add, "The add is after the entry."));
        }

        var previous = entry.Time;
        for (var i = 0; i < scenario.StopMoves.Count; i++)
        {
            if (scenario.StopMoves[i].Time <= previous)
            {
                problems.Add(Problem(ScenarioPointName.StopMove, i, i == 0 ? "A stop move is after the entry." : "The stop moves are in time order."));
            }

            previous = Math.Max(previous, scenario.StopMoves[i].Time);
        }

        previous = entry.Time;
        for (var i = 0; i < scenario.Exits.Count; i++)
        {
            if (scenario.Exits[i].Time < previous)
            {
                problems.Add(Problem(ScenarioPointName.Exit, i, i == 0 ? "An exit is not before the entry." : "The exits are in time order."));
            }

            previous = Math.Max(previous, scenario.Exits[i].Time);
        }
    }

    private static void CheckExits(ExpertScenarioReading scenario, List<ScenarioFormProblem> problems)
    {
        var total = 0.0;
        for (var i = 0; i < scenario.Exits.Count; i++)
        {
            var exit = scenario.Exits[i];
            var field = ScenarioPoints.FieldOf(ScenarioPointName.Exit, i);
            if (!double.IsFinite(exit.Share) || exit.Share <= 0 || exit.Share > 100)
            {
                problems.Add(new ScenarioFormProblem(field + ".share", "A share is more than 0 and at most 100 %."));
            }

            if (!ScenarioCodes.ExitKinds.Contains(exit.Kind))
            {
                problems.Add(new ScenarioFormProblem(field + ".kind", "An exit is at a target or at the stop."));
            }

            total += exit.Share;
        }

        if (scenario.Exits.Count > 0 && Math.Abs(total - 100) > ShareTolerance)
        {
            problems.Add(new ScenarioFormProblem("trade.exits", "The shares of the exits add up to " + Format(total) + " %, not 100 %."));
        }
    }

    private static void CheckPrices(ExpertScenarioReading scenario, List<ScenarioFormProblem> problems)
    {
        foreach (var (name, point) in scenario.Points)
        {
            if (!IsPrice(point.Price))
            {
                problems.Add(Problem(name, "The price is a number above 0."));
            }
        }

        if (scenario.StopPrice is { } stop && !IsPrice(stop))
        {
            problems.Add(new ScenarioFormProblem("trade.stop", "The price is a number above 0."));
        }

        for (var i = 0; i < scenario.StopMoves.Count; i++)
        {
            if (!IsPrice(scenario.StopMoves[i].Price))
            {
                problems.Add(Problem(ScenarioPointName.StopMove, i, "The price is a number above 0."));
            }
        }

        for (var i = 0; i < scenario.Exits.Count; i++)
        {
            if (!IsPrice(scenario.Exits[i].Price))
            {
                problems.Add(Problem(ScenarioPointName.Exit, i, "The price is a number above 0."));
            }
        }
    }

    /// <summary>Every point lies in from..to; a point is its candle's open time, so the candle opening at to is inside.</summary>
    private static void CheckSpan(ExpertScenarioReading scenario, List<ScenarioFormProblem> problems)
    {
        if (scenario.From is not { } from || scenario.To is not { } to || from >= to)
        {
            return;
        }

        foreach (var (name, point) in scenario.Points)
        {
            if (point.Time < from || point.Time > to)
            {
                problems.Add(Problem(name, "The point is outside the time span."));
            }
        }

        for (var i = 0; i < scenario.StopMoves.Count; i++)
        {
            if (scenario.StopMoves[i].Time < from || scenario.StopMoves[i].Time > to)
            {
                problems.Add(Problem(ScenarioPointName.StopMove, i, "The point is outside the time span."));
            }
        }

        for (var i = 0; i < scenario.Exits.Count; i++)
        {
            if (scenario.Exits[i].Time < from || scenario.Exits[i].Time > to)
            {
                problems.Add(Problem(ScenarioPointName.Exit, i, "The point is outside the time span."));
            }
        }
    }

    /// <summary>
    /// The points that are placed follow one another. Internals lie after the point they start from and not after the
    /// candle (on the context) of the point they end at: they sit on smaller candles inside it.
    /// </summary>
    private static void CheckOrder(
        ExpertScenarioReading scenario,
        IReadOnlyList<ScenarioPointName> points,
        ScenarioPointName? lowerBound,
        ScenarioPointName? upperBound,
        List<ScenarioFormProblem> problems)
    {
        long? previous = null;
        if (lowerBound is { } lower && scenario.Points.TryGetValue(lower, out var start))
        {
            previous = start.Time;
        }

        foreach (var name in points)
        {
            if (!scenario.Points.TryGetValue(name, out var point))
            {
                continue;
            }

            if (previous != null && point.Time <= previous)
            {
                problems.Add(Problem(name, Label(name) + " is after the point before it."));
            }

            previous = Math.Max(previous ?? point.Time, point.Time);
        }

        if (upperBound is { } upper && scenario.Points.TryGetValue(upper, out var end) && scenario.ContextTimeframe is { } context && ScenarioLevels.IsContext(context))
        {
            var endOfCandle = Timeframes.CloseTime(context, end.Time);
            foreach (var name in points)
            {
                if (scenario.Points.TryGetValue(name, out var point) && point.Time >= endOfCandle)
                {
                    problems.Add(Problem(name, Label(name) + " is not after " + Label(upper) + "."));
                }
            }
        }
    }

    private static void Require(ExpertScenarioReading scenario, IEnumerable<ScenarioPointName> names, string pattern, List<ScenarioFormProblem> problems)
    {
        foreach (var name in names)
        {
            if (!scenario.Points.ContainsKey(name))
            {
                problems.Add(Problem(name, "Place " + Label(name) + ": " + pattern + " needs it."));
            }
        }
    }

    private static void Forbid(IEnumerable<ScenarioPointName> placed, string pattern, List<ScenarioFormProblem> problems)
    {
        foreach (var name in placed)
        {
            problems.Add(Problem(name, Label(name) + " does not belong to " + pattern + "; EWTS refuses a file with both."));
        }
    }

    private static ScenarioFormProblem Problem(ScenarioPointName name, string message)
    {
        return new ScenarioFormProblem(ScenarioPoints.FieldOf(name), message);
    }

    private static ScenarioFormProblem Problem(ScenarioPointName name, int index, string message)
    {
        return new ScenarioFormProblem(ScenarioPoints.FieldOf(name, index), message);
    }

    private static bool IsPrice(double price)
    {
        return double.IsFinite(price) && price > 0;
    }

    private static string Format(double value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The name of a point in the messages.</summary>
    public static string Label(ScenarioPointName name)
    {
        switch (name)
        {
            case ScenarioPointName.Wave1Start:
                return "the start of wave 1";
            case ScenarioPointName.Wave1End:
                return "the end of wave 1";
            case ScenarioPointName.Wave2End:
                return "the end of wave 2";
            case ScenarioPointName.Wave1I:
                return "i";
            case ScenarioPointName.Wave1Ii:
                return "ii";
            case ScenarioPointName.Wave1Iii:
                return "iii";
            case ScenarioPointName.Wave1Iv:
                return "iv";
            case ScenarioPointName.Wave1V:
                return "v";
            case ScenarioPointName.Wave1A:
            case ScenarioPointName.Wave2A:
                return "a";
            case ScenarioPointName.Wave1B:
            case ScenarioPointName.Wave2B:
                return "b";
            case ScenarioPointName.Wave1C:
            case ScenarioPointName.Wave2C:
                return "c";
            case ScenarioPointName.Entry:
                return "the entry";
            case ScenarioPointName.Add:
                return "the add";
            case ScenarioPointName.Stop:
                return "the stop";
            case ScenarioPointName.StopMove:
                return "a stop move";
            case ScenarioPointName.Exit:
                return "an exit";
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }
    }
}
