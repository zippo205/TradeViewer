using TradeViewer.Contracts.Scenarios;
using TradeViewer.Domain.MarketData;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Services;

/// <summary>The file's record as the Domain sees it for the form checks (SC15): codes parsed, times in Unix seconds, the named points by name.</summary>
public static class ExpertScenarioReadings
{
    public static ExpertScenarioReading Of(ExpertScenarioRecord scenario)
    {
        var points = new Dictionary<ScenarioPointName, ScenarioPoint>();
        void Add(ScenarioPointName name, ScenarioPointRecord? point)
        {
            if (point != null)
            {
                points[name] = Point(point);
            }
        }

        Add(ScenarioPointName.Wave1Start, scenario.Waves?.Wave1Start);
        Add(ScenarioPointName.Wave1End, scenario.Waves?.Wave1End);
        Add(ScenarioPointName.Wave2End, scenario.Waves?.Wave2End);

        var wave1 = scenario.Wave1;
        Add(ScenarioPointName.Wave1I, wave1?.IEnd);
        Add(ScenarioPointName.Wave1Ii, wave1?.IiEnd);
        Add(ScenarioPointName.Wave1Iii, wave1?.IiiEnd);
        Add(ScenarioPointName.Wave1Iv, wave1?.IvEnd);
        Add(ScenarioPointName.Wave1V, wave1?.VEnd);
        Add(ScenarioPointName.Wave1A, wave1?.AEnd);
        Add(ScenarioPointName.Wave1B, wave1?.BEnd);
        Add(ScenarioPointName.Wave1C, wave1?.CEnd);

        Add(ScenarioPointName.Wave2A, scenario.Wave2?.AEnd);
        Add(ScenarioPointName.Wave2B, scenario.Wave2?.BEnd);
        Add(ScenarioPointName.Wave2C, scenario.Wave2?.CEnd);

        var trade = scenario.Trade;
        Add(ScenarioPointName.Entry, trade?.Entry);
        Add(ScenarioPointName.Add, trade?.Add);

        return new ExpertScenarioReading(
            Timeframes.TryParse(scenario.ContextTimeframe, out var context) ? context : null,
            scenario.Direction,
            scenario.Verdict,
            scenario.Reason,
            Seconds(scenario.From),
            Seconds(scenario.To),
            points,
            wave1 != null,
            wave1?.Pattern,
            scenario.Wave2 != null,
            scenario.Wave2?.Pattern,
            trade != null,
            trade?.Stop?.Price,
            (trade?.StopMoves ?? Array.Empty<ScenarioPointRecord>()).Select(Point).ToList(),
            (trade?.Exits ?? Array.Empty<ScenarioExitRecord>()).Select(exit => new ScenarioExit(exit.Time.ToUnixTimeSeconds(), exit.Price, exit.Share, exit.Kind)).ToList());
    }

    private static ScenarioPoint Point(ScenarioPointRecord point)
    {
        return new ScenarioPoint(point.Time.ToUnixTimeSeconds(), point.Price);
    }

    private static long? Seconds(DateTimeOffset? time)
    {
        return time?.ToUnixTimeSeconds();
    }
}
