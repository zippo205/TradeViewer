using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Web.Scenarios;

/// <summary>A point tool as the editor offers it (SC10): its button text, its tooltip and what it hands the chart module.</summary>
public sealed record ScenarioPointTool(string Label, string Title, ScenarioTool Tool);

/// <summary>A named point of a scenario as the panel lists it: its key (field, "#index" in a list), its level and its value.</summary>
public sealed record ScenarioPointEntry(string Key, string Field, int? Index, string Level, string Label, DateTimeOffset? Time, double Price, string? Why);

/// <summary>
/// The named points of an expert scenario in the editor (SC10, SC11, decision 0012): which point belongs to which
/// level, the tools offered on a level for a verdict, the handles the chart gets, and removing a point. The server
/// snaps and places the points (ScenarioPointEditor); this side only knows the fields of the file.
/// </summary>
public static class ScenarioPoints
{
    public const string Context = "context";
    public const string Setup = "setup";
    public const string Trigger = "trigger";

    public const string StopMoves = "trade.stopMoves";
    public const string Exits = "trade.exits";

    public static readonly IReadOnlyList<string> Verdicts = new[] { "trade-win", "trade-loss", "no-trade" };
    public static readonly IReadOnlyList<string> Wave1Patterns = new[] { "impulse", "leading-diagonal", "three" };
    public static readonly IReadOnlyList<string> Wave2Patterns = new[] { "zigzag", "flat", "expanded-flat", "running-flat", "triangle", "complex" };
    public static readonly IReadOnlyList<string> ExitKinds = new[] { "target", "stop" };

    private static readonly string[] Waves = { "waves.wave1Start", "waves.wave1End", "waves.wave2End" };
    private static readonly string[] Wave1Five = { "wave1.iEnd", "wave1.iiEnd", "wave1.iiiEnd", "wave1.ivEnd", "wave1.vEnd" };
    private static readonly string[] Wave1Three = { "wave1.aEnd", "wave1.bEnd", "wave1.cEnd" };
    private static readonly string[] Wave2 = { "wave2.aEnd", "wave2.bEnd", "wave2.cEnd" };

    /// <summary>The level a field belongs to: the waves to the context, the internals to the setup, the trade to the trigger.</summary>
    public static string LevelOf(string field)
    {
        return field.StartsWith("waves.", StringComparison.Ordinal) ? Context
            : field.StartsWith("trade.", StringComparison.Ordinal) ? Trigger
            : Setup;
    }

    public static bool HasTrade(string? verdict) => verdict == "trade-win" || verdict == "trade-loss";

    public static bool IsFive(string? pattern) => pattern == "impulse" || pattern == "leading-diagonal";

    /// <summary>
    /// The tools of a level (SC10): "1-2" on the context; "i-v" and "a-c" of wave 1 and "a-c" of wave 2 on the setup;
    /// the trade tools on the trigger for trade-win and trade-loss only. A tool of several points places the missing
    /// ones of its group, or all of them again when none is missing. Wave 1 offers only the tool of its pattern.
    /// </summary>
    public static IReadOnlyList<ScenarioPointTool> ToolsOf(string level, ExpertScenarioRecord scenario)
    {
        var tools = new List<ScenarioPointTool>();
        switch (level)
        {
            case Context:
                tools.Add(Group("oneTwo", "1-2", "Place wave 1 start, wave 1 end and wave 2 end, one click each", Waves, scenario));
                break;

            case Setup:
                var pattern = scenario.Wave1?.Pattern;
                if (pattern == null || IsFive(pattern))
                {
                    tools.Add(Group("wave1Five", "1: i-v", "Place the internals i to v of wave 1 (v is optional: without it wave 1 end is v)", Wave1Five, scenario));
                }

                if (pattern == null || pattern == "three")
                {
                    tools.Add(Group("wave1Three", "1: a-c", "Place the internals a to c of wave 1 as a three (c is optional)", Wave1Three, scenario));
                }

                tools.Add(Group("wave2", "2: a-c", "Place the internals a to c of wave 2 (c is optional: without it wave 2 end is c)", Wave2, scenario));
                break;

            case Trigger when HasTrade(scenario.Verdict):
                tools.Add(Single("entry", "Entry", "Place the entry: the candle that closes beyond the trigger", "trade.entry"));
                tools.Add(Single("stop", "Stop", "Set the first stop at a price", "trade.stop"));
                tools.Add(Single("add", "Add", "Place the add", "trade.add"));
                tools.Add(Single("stopMove", "Stop move", "Place a stop move at the candle whose close led to it", StopMoves));
                tools.Add(Single("exitTarget", "Exit target", "Place an exit at a target", Exits, "target"));
                tools.Add(Single("exitStop", "Exit stop", "Place an exit at the stop", Exits, "stop"));
                break;
        }

        return tools;
    }

    /// <summary>Every named point of the scenario, in the order of the file.</summary>
    public static IReadOnlyList<ScenarioPointEntry> EntriesOf(ExpertScenarioRecord scenario)
    {
        var entries = new List<ScenarioPointEntry>();
        void Add(string field, string label, ScenarioPointRecord? point)
        {
            if (point != null)
            {
                entries.Add(new ScenarioPointEntry(field, field, null, LevelOf(field), label, point.Time, point.Price, point.Why));
            }
        }

        Add("waves.wave1Start", "Wave 1 start", scenario.Waves?.Wave1Start);
        Add("waves.wave1End", "Wave 1 end", scenario.Waves?.Wave1End);
        Add("waves.wave2End", "Wave 2 end", scenario.Waves?.Wave2End);
        var wave1 = scenario.Wave1;
        Add("wave1.iEnd", "Wave 1: i", wave1?.IEnd);
        Add("wave1.iiEnd", "Wave 1: ii", wave1?.IiEnd);
        Add("wave1.iiiEnd", "Wave 1: iii", wave1?.IiiEnd);
        Add("wave1.ivEnd", "Wave 1: iv", wave1?.IvEnd);
        Add("wave1.vEnd", "Wave 1: v", wave1?.VEnd);
        Add("wave1.aEnd", "Wave 1: a", wave1?.AEnd);
        Add("wave1.bEnd", "Wave 1: b", wave1?.BEnd);
        Add("wave1.cEnd", "Wave 1: c", wave1?.CEnd);
        Add("wave2.aEnd", "Wave 2: a", scenario.Wave2?.AEnd);
        Add("wave2.bEnd", "Wave 2: b", scenario.Wave2?.BEnd);
        Add("wave2.cEnd", "Wave 2: c", scenario.Wave2?.CEnd);

        var trade = scenario.Trade;
        Add("trade.entry", "Entry", trade?.Entry);
        if (trade?.Stop != null)
        {
            entries.Add(new ScenarioPointEntry("trade.stop", "trade.stop", null, Trigger, "Stop", null, trade.Stop.Price, trade.Stop.Why));
        }

        Add("trade.add", "Add", trade?.Add);
        for (var i = 0; i < (trade?.StopMoves?.Count ?? 0); i++)
        {
            var move = trade!.StopMoves![i];
            entries.Add(new ScenarioPointEntry(StopMoves + "#" + i, StopMoves, i, Trigger, "Stop move " + (i + 1), move.Time, move.Price, move.Why));
        }

        for (var i = 0; i < (trade?.Exits?.Count ?? 0); i++)
        {
            var exit = trade!.Exits![i];
            entries.Add(new ScenarioPointEntry(Exits + "#" + i, Exits, i, Trigger, "Exit " + (i + 1), exit.Time, exit.Price, exit.Why));
        }

        return entries;
    }

    /// <summary>
    /// The handles the chart gets (setScenarioPoints): the points of the level shown are editable, the others are drawn
    /// by the shapes only (decision 0012). The stop has no time of its own; its handle sits at the entry.
    /// </summary>
    public static IReadOnlyList<ScenarioHandle> HandlesOf(ExpertScenarioRecord scenario, string shownLevel, bool editable)
    {
        var anchor = scenario.Trade?.Entry?.Time ?? scenario.Waves?.Wave2End?.Time;
        return EntriesOf(scenario)
            .Where(entry => entry.Time != null || anchor != null)
            .Select(entry => new ScenarioHandle(entry.Field, entry.Index, (entry.Time ?? anchor)!.Value.ToUnixTimeSeconds(), entry.Price, editable && entry.Level == shownLevel))
            .ToList();
    }

    /// <summary>The field in the file of a problem's point: "trade.exits[1]" for the key "trade.exits#1".</summary>
    public static string ProblemFieldOf(string key)
    {
        var hash = key.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? key : key.Substring(0, hash) + "[" + key.Substring(hash + 1) + "]";
    }

    /// <summary>The scenario without the point of the key (Delete, SC10). An unknown key changes nothing.</summary>
    public static ExpertScenarioRecord Remove(ExpertScenarioRecord scenario, string key)
    {
        var hash = key.IndexOf('#', StringComparison.Ordinal);
        var field = hash < 0 ? key : key.Substring(0, hash);
        var index = hash < 0 ? -1 : int.Parse(key.Substring(hash + 1), System.Globalization.CultureInfo.InvariantCulture);
        var waves = scenario.Waves;
        var wave1 = scenario.Wave1;
        var wave2 = scenario.Wave2;
        var trade = scenario.Trade;
        return field switch
        {
            "waves.wave1Start" when waves != null => scenario with { Waves = waves with { Wave1Start = null } },
            "waves.wave1End" when waves != null => scenario with { Waves = waves with { Wave1End = null } },
            "waves.wave2End" when waves != null => scenario with { Waves = waves with { Wave2End = null } },
            "wave1.iEnd" when wave1 != null => scenario with { Wave1 = wave1 with { IEnd = null } },
            "wave1.iiEnd" when wave1 != null => scenario with { Wave1 = wave1 with { IiEnd = null } },
            "wave1.iiiEnd" when wave1 != null => scenario with { Wave1 = wave1 with { IiiEnd = null } },
            "wave1.ivEnd" when wave1 != null => scenario with { Wave1 = wave1 with { IvEnd = null } },
            "wave1.vEnd" when wave1 != null => scenario with { Wave1 = wave1 with { VEnd = null } },
            "wave1.aEnd" when wave1 != null => scenario with { Wave1 = wave1 with { AEnd = null } },
            "wave1.bEnd" when wave1 != null => scenario with { Wave1 = wave1 with { BEnd = null } },
            "wave1.cEnd" when wave1 != null => scenario with { Wave1 = wave1 with { CEnd = null } },
            "wave2.aEnd" when wave2 != null => scenario with { Wave2 = wave2 with { AEnd = null } },
            "wave2.bEnd" when wave2 != null => scenario with { Wave2 = wave2 with { BEnd = null } },
            "wave2.cEnd" when wave2 != null => scenario with { Wave2 = wave2 with { CEnd = null } },
            "trade.entry" when trade != null => scenario with { Trade = trade with { Entry = null } },
            "trade.stop" when trade != null => scenario with { Trade = trade with { Stop = null } },
            "trade.add" when trade != null => scenario with { Trade = trade with { Add = null } },
            StopMoves when trade?.StopMoves != null && index >= 0 && index < trade.StopMoves.Count
                => scenario with { Trade = trade with { StopMoves = Without(trade.StopMoves, index) } },
            Exits when trade?.Exits != null && index >= 0 && index < trade.Exits.Count
                => scenario with { Trade = trade with { Exits = Without(trade.Exits, index) } },
            _ => scenario
        };
    }

    /// <summary>The scenario with another reason for the point of the key (SC12); an empty text removes it.</summary>
    public static ExpertScenarioRecord WithWhy(ExpertScenarioRecord scenario, string key, string? why)
    {
        why = string.IsNullOrWhiteSpace(why) ? null : why.Trim();
        var hash = key.IndexOf('#', StringComparison.Ordinal);
        var field = hash < 0 ? key : key.Substring(0, hash);
        var index = hash < 0 ? -1 : int.Parse(key.Substring(hash + 1), System.Globalization.CultureInfo.InvariantCulture);
        ScenarioPointRecord? Why(ScenarioPointRecord? point) => point == null ? null : point with { Why = why };
        var waves = scenario.Waves;
        var wave1 = scenario.Wave1;
        var wave2 = scenario.Wave2;
        var trade = scenario.Trade;
        return field switch
        {
            "waves.wave1Start" when waves != null => scenario with { Waves = waves with { Wave1Start = Why(waves.Wave1Start) } },
            "waves.wave1End" when waves != null => scenario with { Waves = waves with { Wave1End = Why(waves.Wave1End) } },
            "waves.wave2End" when waves != null => scenario with { Waves = waves with { Wave2End = Why(waves.Wave2End) } },
            "wave1.iEnd" when wave1 != null => scenario with { Wave1 = wave1 with { IEnd = Why(wave1.IEnd) } },
            "wave1.iiEnd" when wave1 != null => scenario with { Wave1 = wave1 with { IiEnd = Why(wave1.IiEnd) } },
            "wave1.iiiEnd" when wave1 != null => scenario with { Wave1 = wave1 with { IiiEnd = Why(wave1.IiiEnd) } },
            "wave1.ivEnd" when wave1 != null => scenario with { Wave1 = wave1 with { IvEnd = Why(wave1.IvEnd) } },
            "wave1.vEnd" when wave1 != null => scenario with { Wave1 = wave1 with { VEnd = Why(wave1.VEnd) } },
            "wave1.aEnd" when wave1 != null => scenario with { Wave1 = wave1 with { AEnd = Why(wave1.AEnd) } },
            "wave1.bEnd" when wave1 != null => scenario with { Wave1 = wave1 with { BEnd = Why(wave1.BEnd) } },
            "wave1.cEnd" when wave1 != null => scenario with { Wave1 = wave1 with { CEnd = Why(wave1.CEnd) } },
            "wave2.aEnd" when wave2 != null => scenario with { Wave2 = wave2 with { AEnd = Why(wave2.AEnd) } },
            "wave2.bEnd" when wave2 != null => scenario with { Wave2 = wave2 with { BEnd = Why(wave2.BEnd) } },
            "wave2.cEnd" when wave2 != null => scenario with { Wave2 = wave2 with { CEnd = Why(wave2.CEnd) } },
            "trade.entry" when trade != null => scenario with { Trade = trade with { Entry = Why(trade.Entry) } },
            "trade.stop" when trade?.Stop != null => scenario with { Trade = trade with { Stop = trade.Stop with { Why = why } } },
            "trade.add" when trade != null => scenario with { Trade = trade with { Add = Why(trade.Add) } },
            StopMoves when trade?.StopMoves != null && index >= 0 && index < trade.StopMoves.Count
                => scenario with { Trade = trade with { StopMoves = Replaced(trade.StopMoves, index, trade.StopMoves[index] with { Why = why }) } },
            Exits when trade?.Exits != null && index >= 0 && index < trade.Exits.Count
                => scenario with { Trade = trade with { Exits = Replaced(trade.Exits, index, trade.Exits[index] with { Why = why }) } },
            _ => scenario
        };
    }

    /// <summary>The scenario with an exit changed in the panel: its price, share and kind (SC12).</summary>
    public static ExpertScenarioRecord WithExit(ExpertScenarioRecord scenario, int index, ScenarioExitRecord exit)
    {
        var trade = scenario.Trade;
        if (trade?.Exits == null || index < 0 || index >= trade.Exits.Count)
        {
            return scenario;
        }

        return scenario with { Trade = trade with { Exits = Replaced(trade.Exits, index, exit) } };
    }

    /// <summary>The points of wave 1 the pattern would drop: those of a three for a five and the other way round (proposal 9).</summary>
    public static int Wave1PointsDroppedBy(ExpertScenarioRecord scenario, string? pattern)
    {
        var wave1 = scenario.Wave1;
        if (wave1 == null || pattern == null)
        {
            return 0;
        }

        var five = new[] { wave1.IEnd, wave1.IiEnd, wave1.IiiEnd, wave1.IvEnd, wave1.VEnd }.Count(point => point != null);
        var three = new[] { wave1.AEnd, wave1.BEnd, wave1.CEnd }.Count(point => point != null);
        return IsFive(pattern) ? three : five;
    }

    /// <summary>The scenario with another pattern of wave 1; the points of the other kind are removed (a file with both is invalid for EWTS).</summary>
    public static ExpertScenarioRecord WithWave1Pattern(ExpertScenarioRecord scenario, string? pattern, string setupTimeframe)
    {
        var wave1 = scenario.Wave1 ?? new ScenarioWave1Record(setupTimeframe, null);
        if (pattern == null)
        {
            return scenario with { Wave1 = wave1 with { Pattern = null } };
        }

        wave1 = IsFive(pattern)
            ? wave1 with { Pattern = pattern, AEnd = null, BEnd = null, CEnd = null }
            : wave1 with { Pattern = pattern, IEnd = null, IiEnd = null, IiiEnd = null, IvEnd = null, VEnd = null };
        return scenario with { Wave1 = wave1 };
    }

    /// <summary>The scenario with another pattern of wave 2.</summary>
    public static ExpertScenarioRecord WithWave2Pattern(ExpertScenarioRecord scenario, string? pattern, string setupTimeframe)
    {
        var wave2 = scenario.Wave2 ?? new ScenarioWave2Record(setupTimeframe, null);
        return scenario with { Wave2 = wave2 with { Pattern = pattern } };
    }

    private static ScenarioPointTool Group(string code, string label, string title, string[] fields, ExpertScenarioRecord scenario)
    {
        var present = EntriesOf(scenario).Select(entry => entry.Field).ToHashSet(StringComparer.Ordinal);
        var missing = fields.Where(field => !present.Contains(field)).ToList();
        return new ScenarioPointTool(label, title, new ScenarioTool(code, missing.Count > 0 ? missing : fields));
    }

    private static ScenarioPointTool Single(string code, string label, string title, string field, string? kind = null)
    {
        return new ScenarioPointTool(label, title, new ScenarioTool(code, new[] { field }, kind));
    }

    private static IReadOnlyList<T> Without<T>(IReadOnlyList<T> items, int index)
    {
        return items.Where((_, at) => at != index).ToList();
    }

    private static IReadOnlyList<T> Replaced<T>(IReadOnlyList<T> items, int index, T item)
    {
        return items.Select((old, at) => at == index ? item : old).ToList();
    }
}
