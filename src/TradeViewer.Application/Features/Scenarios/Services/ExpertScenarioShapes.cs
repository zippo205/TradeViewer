using System.Globalization;
using TradeViewer.Contracts.Overlays;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Domain.MarketData;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Services;

/// <summary>
/// The picture of an expert scenario, built from its named points every time (SC8, decision 0010): no drawing of the
/// points is stored. Four groups, each on its level (SC21):
/// <list type="bullet">
/// <item>waves (context): a strong line wave 1 start, wave 1 end, wave 2 end with the labels 0, 1, 2;</item>
/// <item>wave 1 (setup): the line from the start of wave 1 through i to v, or a to c, with their labels;</item>
/// <item>wave 2 (setup): the line from the end of wave 1 through a to c, with their labels;</item>
/// <item>trade (trigger): markers for entry, add and the exits; the stop as a level from the entry, stepping at
/// each stop move, to the last exit.</item>
/// </list>
/// Every shape of a point carries its price, time and reason as the detail shown on pointing.
/// </summary>
public static class ExpertScenarioShapes
{
    public static IReadOnlyList<OverlayGroupRecord> Of(ExpertScenarioRecord scenario)
    {
        var context = Timeframes.TryParse(scenario.ContextTimeframe, out var parsed) && ScenarioLevels.IsContext(parsed) ? parsed : (Timeframe?)null;
        var groups = new List<OverlayGroupRecord>();

        var waves = scenario.Waves;
        if (waves != null)
        {
            groups.Add(Group("waves", "Waves 1 and 2", "waves", waves.Timeframe, context, ScenarioLevel.Context, Path(new[]
            {
                ("0", waves.Wave1Start, "below"),
                ("1", waves.Wave1End, "above"),
                ("2", waves.Wave2End, "below")
            }, "wave", "strong")));
        }

        var wave1 = scenario.Wave1;
        if (wave1 != null)
        {
            var three = wave1.Pattern == ScenarioCodes.Three;
            var points = three
                ? new[] { ("a", wave1.AEnd, "above"), ("b", wave1.BEnd, "below"), ("c", wave1.CEnd, "above") }
                : new[] { ("i", wave1.IEnd, "above"), ("ii", wave1.IiEnd, "below"), ("iii", wave1.IiiEnd, "above"), ("iv", wave1.IvEnd, "below"), ("v", wave1.VEnd, "above") };
            var lastEnd = three ? wave1.CEnd : wave1.VEnd;
            groups.Add(Group("wave1", "Wave 1 (" + (wave1.Pattern ?? "no pattern") + ")", "lowerDegree", wave1.Timeframe, context, ScenarioLevel.Setup,
                Internals(wave1.Start ?? waves?.Wave1Start, points, lastEnd == null ? waves?.Wave1End : null)));
        }

        var wave2 = scenario.Wave2;
        if (wave2 != null)
        {
            var points = new[] { ("a", wave2.AEnd, "below"), ("b", wave2.BEnd, "above"), ("c", wave2.CEnd, "below") };
            groups.Add(Group("wave2", "Wave 2 (" + (wave2.Pattern ?? "no pattern") + ")", "lowerDegree", wave2.Timeframe, context, ScenarioLevel.Setup,
                Internals(wave2.Start ?? waves?.Wave1End, points, wave2.CEnd == null ? waves?.Wave2End : null)));
        }

        var trade = scenario.Trade;
        if (trade != null)
        {
            groups.Add(Group("trade", "Trade", "trade", trade.Timeframe, context, ScenarioLevel.Trigger, Trade(trade, scenario.To)));
        }

        return groups;
    }

    /// <summary>The level a section is drawn on: the one of its timeframe, else its own level, else the context (decision 0012, point 2).</summary>
    private static OverlayGroupRecord Group(string key, string title, string kind, string? timeframe, Timeframe? context, ScenarioLevel own, IReadOnlyList<OverlayShapeRecord> shapes)
    {
        var level = own;
        if (context != null && Timeframes.TryParse(timeframe, out var sectionTimeframe))
        {
            level = ScenarioLevels.LevelOf(context.Value, sectionTimeframe) ?? ScenarioLevel.Context;
        }

        return new OverlayGroupRecord(key, title, kind, timeframe, shapes, ScenarioLevels.Code(level));
    }

    /// <summary>A line through the points that are placed and a label at each.</summary>
    private static IReadOnlyList<OverlayShapeRecord> Path(IEnumerable<(string Label, ScenarioPointRecord? Point, string Side)> points, string role, string? weight)
    {
        var placed = points.Where(point => point.Point != null).ToList();
        var shapes = new List<OverlayShapeRecord>();
        if (placed.Count >= 2)
        {
            shapes.Add(new OverlayShapeRecord("line", role, Points: placed.Select(point => new OverlayPointRecord(point.Point!.Time, point.Point.Price)).ToList(), Weight: weight));
        }

        foreach (var (label, point, side) in placed)
        {
            shapes.Add(new OverlayShapeRecord("label", role, Time: point!.Time, Price: point.Price, Text: label, Side: side, Detail: Detail(label, point)));
        }

        return shapes;
    }

    /// <summary>The internals of a wave: the line starts at the wave's start and, without its last internal point, ends at the wave's end.</summary>
    private static IReadOnlyList<OverlayShapeRecord> Internals(ScenarioPointRecord? start, (string, ScenarioPointRecord?, string)[] points, ScenarioPointRecord? end)
    {
        var shapes = new List<OverlayShapeRecord>();
        var line = new List<ScenarioPointRecord>();
        if (start != null)
        {
            line.Add(start);
        }

        line.AddRange(points.Where(point => point.Item2 != null).Select(point => point.Item2!));
        if (end != null && line.Count > (start == null ? 0 : 1))
        {
            line.Add(end);
        }

        if (line.Count >= 2)
        {
            shapes.Add(new OverlayShapeRecord("line", "lowerDegree", Points: line.Select(point => new OverlayPointRecord(point.Time, point.Price)).ToList()));
        }

        foreach (var (label, point, side) in points.Where(point => point.Item2 != null))
        {
            shapes.Add(new OverlayShapeRecord("label", "lowerDegree", Time: point!.Time, Price: point.Price, Text: label, Side: side, Detail: Detail(label, point)));
        }

        return shapes;
    }

    private static IReadOnlyList<OverlayShapeRecord> Trade(ScenarioTradeRecord trade, DateTimeOffset? to)
    {
        var shapes = new List<OverlayShapeRecord>();
        var exits = trade.Exits ?? Array.Empty<ScenarioExitRecord>();
        var end = exits.Count > 0 ? exits.Max(exit => exit.Time) : to;

        // Dotted lines make the prices of the trade easy to see (the owner, 2026-11-21): entry and add to the end of the
        // trade, each exit from the entry to where it was taken. A line has the key of its point, so a click on it
        // selects that point like its marker.
        if (trade.Entry != null)
        {
            shapes.Add(Line("entry", trade.Entry.Price, trade.Entry.Time, end, Detail("Entry", trade.Entry), "trade.entry"));
            shapes.Add(Marker("entry", "Entry", trade.Entry, "trade.entry"));
        }

        if (trade.Add != null)
        {
            shapes.Add(Line("add", trade.Add.Price, trade.Add.Time, end, Detail("Add", trade.Add), "trade.add"));
            shapes.Add(Marker("add", "Add", trade.Add, "trade.add"));
        }
        if (trade.Stop != null && trade.Entry != null)
        {
            var moves = (trade.StopMoves ?? Array.Empty<ScenarioPointRecord>()).Select((move, index) => (Move: move, Index: index)).OrderBy(move => move.Move.Time).ToList();
            var steps = new List<(DateTimeOffset From, double Price, string Text, string? Why, string Key)> { (trade.Entry.Time, trade.Stop.Price, "Stop", trade.Stop.Why, "trade.stop") };
            steps.AddRange(moves.Select(move => (move.Move.Time, move.Move.Price, "Stop moved", move.Move.Why, "trade.stopMoves#" + move.Index)));
            for (var i = 0; i < steps.Count; i++)
            {
                var stepEnd = i + 1 < steps.Count ? steps[i + 1].From : end;
                var detail = steps[i].Text + ": " + Price(steps[i].Price) + " from " + Time(steps[i].From) + Why(steps[i].Why);
                shapes.Add(new OverlayShapeRecord("level", "stop", Price: steps[i].Price, From: steps[i].From, To: stepEnd, Text: steps[i].Text, Detail: detail, Key: steps[i].Key));
            }
        }

        for (var index = 0; index < exits.Count; index++)
        {
            var exit = exits[index];
            var text = "Exit " + exit.Share.ToString("0.##", CultureInfo.InvariantCulture) + " %";
            var role = exit.Kind == ScenarioCodes.StopKind ? "stop" : "target";
            var detail = text + " (" + exit.Kind + "): " + Price(exit.Price) + " at " + Time(exit.Time) + Why(exit.Why);
            var from = trade.Entry != null && trade.Entry.Time < exit.Time ? trade.Entry.Time : exit.Time;
            shapes.Add(Line(role, exit.Price, from, exit.Time, detail, "trade.exits#" + index));
            shapes.Add(new OverlayShapeRecord("marker", role, Time: exit.Time, Price: exit.Price, Text: text, Detail: detail, Key: "trade.exits#" + index));
        }

        return shapes;
    }

    /// <summary>A dotted level at a price of the trade, without a text (its marker carries it).</summary>
    private static OverlayShapeRecord Line(string role, double price, DateTimeOffset from, DateTimeOffset? to, string detail, string key)
    {
        return new OverlayShapeRecord("level", role, Price: price, From: from, To: to, Detail: detail, Style: "dotted", Key: key);
    }

    private static OverlayShapeRecord Marker(string role, string text, ScenarioPointRecord point, string key)
    {
        return new OverlayShapeRecord("marker", role, Time: point.Time, Price: point.Price, Text: text, Detail: Detail(text, point), Key: key);
    }

    private static string Detail(string label, ScenarioPointRecord point)
    {
        return label + ": " + Price(point.Price) + " at " + Time(point.Time) + Why(point.Why);
    }

    private static string Why(string? why)
    {
        return string.IsNullOrWhiteSpace(why) ? string.Empty : " - " + why;
    }

    private static string Price(double price)
    {
        return price.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static string Time(DateTimeOffset time)
    {
        return time.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
    }
}
