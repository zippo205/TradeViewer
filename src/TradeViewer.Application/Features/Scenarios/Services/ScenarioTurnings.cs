using System.Globalization;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Domain.MarketData;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Services;

/// <summary>
/// The turning points across the levels of an expert scenario (decision 0013), by EWTS's rule (ScenarioTurning): the
/// start of a nested wave derived from its context point, the check that the levels fit before a save, and the points
/// of the higher levels on the candles of the timeframe shown. Reads the candles of the smaller timeframe inside one
/// candle of the larger per point.
/// </summary>
public sealed class ScenarioTurnings
{
    private readonly ICandleSeriesReader _candles;

    public ScenarioTurnings(ICandleSeriesReader candles)
    {
        _candles = candles;
    }

    /// <summary>
    /// After a point of a nested wave was placed: its start, when it is missing and the context point exists, is the
    /// candle the rule names, so the user does not click it twice. Without such a candle nothing is added; the check
    /// before a save then names the gap.
    /// </summary>
    public async Task<ExpertScenarioRecord> DeriveStartAsync(ExpertScenarioRecord scenario, ScenarioPointName placed, CancellationToken cancellationToken)
    {
        if ((ScenarioPoints.Wave1Five.Contains(placed) || ScenarioPoints.Wave1Three.Contains(placed)) && scenario.Wave1 is { Start: null } wave1)
        {
            var start = await TurningAsync(scenario, ScenarioPointName.Wave1Origin, ScenarioPointName.Wave1Start, cancellationToken);
            return start == null ? scenario : scenario with { Wave1 = wave1 with { Start = start } };
        }

        if (ScenarioPoints.Wave2Points.Contains(placed) && scenario.Wave2 is { Start: null } wave2)
        {
            var start = await TurningAsync(scenario, ScenarioPointName.Wave2Origin, ScenarioPointName.Wave1End, cancellationToken);
            return start == null ? scenario : scenario with { Wave2 = wave2 with { Start = start } };
        }

        return scenario;
    }

    /// <summary>
    /// Whether the levels fit (SC15, decision 0013): every start and end of a nested wave that is present must be the
    /// candle the rule names for its context point. Each misfit blocks a save.
    /// </summary>
    public async Task<IReadOnlyList<ScenarioProblemRecord>> MisfitsAsync(ExpertScenarioRecord scenario, CancellationToken cancellationToken)
    {
        var problems = new List<ScenarioProblemRecord>();
        foreach (var (nested, context) in ScenarioPoints.Turnings)
        {
            var point = PointOf(scenario, nested);
            if (point == null)
            {
                continue;
            }

            var field = ScenarioPoints.FieldOf(nested);
            var label = ScenarioFormRules.Label(nested);
            var contextLabel = ScenarioFormRules.Label(context);
            if (PointOf(scenario, context) == null)
            {
                problems.Add(Misfit(field, "Place " + contextLabel + " on the context: " + label + " is the same turning point on a smaller timeframe."));
                continue;
            }

            var expected = await TurningAsync(scenario, nested, context, cancellationToken);
            var timeframe = ScenarioPointEditor.SnapTimeframe(scenario, nested);
            var code = timeframe == null ? "?" : Timeframes.Code(timeframe.Value);
            if (expected == null)
            {
                problems.Add(Misfit(field, "No " + code + " candle inside the candle of " + contextLabel + " has its price (a gap in the data?), so " + label + " cannot be checked and the scenario cannot be saved."));
            }
            else if (expected.Time != point.Time || !Same(expected.Price, point.Price))
            {
                problems.Add(Misfit(field, Capital(label) + " does not fit " + contextLabel + ": on " + code + " it is the candle of " + Text(expected.Time) + " at " + Price(expected.Price) + "."));
            }
        }

        return problems;
    }

    /// <summary>
    /// The scenario as the chart of a smaller timeframe shows it (SC8): every wave point of a level whose timeframe is
    /// larger than the one shown sits on the candle of the shown timeframe that made it; its price stays. Only for the
    /// view; nothing of it is saved.
    /// </summary>
    public async Task<ExpertScenarioRecord> ForViewAsync(ExpertScenarioRecord scenario, Timeframe? shown, CancellationToken cancellationToken)
    {
        if (shown == null)
        {
            return scenario;
        }

        var result = scenario;
        foreach (var name in Enum.GetValues<ScenarioPointName>())
        {
            var kind = ScenarioPoints.SnapOf(name);
            if (kind != ScenarioSnapKind.Low && kind != ScenarioSnapKind.High)
            {
                continue;
            }

            var point = PointOf(scenario, name);
            var own = ScenarioPointEditor.SnapTimeframe(scenario, name);
            if (point == null || own == null || Timeframes.Seconds(own.Value) <= Timeframes.Seconds(shown.Value))
            {
                continue;
            }

            var open = point.Time.ToUnixTimeSeconds();
            var candles = await InsideAsync(scenario, own.Value, open, shown.Value, cancellationToken);
            var candle = candles == null ? null : ScenarioTurning.ForView(candles, open, Timeframes.CloseTime(own.Value, open), point.Price, kind);
            if (candle != null)
            {
                result = WithTime(result, name, DateTimeOffset.FromUnixTimeSeconds(candle.Value.Time));
            }
        }

        return result;
    }

    /// <summary>The point the rule names for a nested point from its context point, on the nested point's timeframe; null without one.</summary>
    private async Task<ScenarioPointRecord?> TurningAsync(ExpertScenarioRecord scenario, ScenarioPointName nested, ScenarioPointName context, CancellationToken cancellationToken)
    {
        var at = PointOf(scenario, context);
        var large = ScenarioPointEditor.SnapTimeframe(scenario, context);
        var small = ScenarioPointEditor.SnapTimeframe(scenario, nested);
        if (at == null || large == null || small == null || Timeframes.Seconds(small.Value) >= Timeframes.Seconds(large.Value))
        {
            return null;
        }

        var open = at.Time.ToUnixTimeSeconds();
        var candles = await InsideAsync(scenario, large.Value, open, small.Value, cancellationToken);
        var candle = candles == null ? null : ScenarioTurning.Find(candles, open, Timeframes.CloseTime(large.Value, open), at.Price, ScenarioPoints.SnapOf(context));
        return candle == null ? null : new ScenarioPointRecord(DateTimeOffset.FromUnixTimeSeconds(candle.Value.Time), at.Price);
    }

    /// <summary>The candles of the smaller timeframe whose open lies inside one candle of the larger; null when they cannot be read.</summary>
    private async Task<IReadOnlyList<Candle>?> InsideAsync(ExpertScenarioRecord scenario, Timeframe large, long open, Timeframe small, CancellationToken cancellationToken)
    {
        var close = Timeframes.CloseTime(large, open);
        var count = (int)Math.Min(ICandleSeriesReader.MaxLimit, ((close - open) / Timeframes.Seconds(small)) + 2);
        var page = await _candles.ReadAsync(scenario.Source, scenario.Symbol, Timeframes.Code(small), close, count, warmUp: 0, cancellationToken);
        return page.IsFailure ? null : page.Value.Candles.Where(candle => candle.Time >= open && candle.Time < close).ToList();
    }

    private static ScenarioPointRecord? PointOf(ExpertScenarioRecord scenario, ScenarioPointName name)
    {
        var waves = scenario.Waves;
        var wave1 = scenario.Wave1;
        var wave2 = scenario.Wave2;
        return name switch
        {
            ScenarioPointName.Wave1Start => waves?.Wave1Start,
            ScenarioPointName.Wave1End => waves?.Wave1End,
            ScenarioPointName.Wave2End => waves?.Wave2End,
            ScenarioPointName.Wave1Origin => wave1?.Start,
            ScenarioPointName.Wave1I => wave1?.IEnd,
            ScenarioPointName.Wave1Ii => wave1?.IiEnd,
            ScenarioPointName.Wave1Iii => wave1?.IiiEnd,
            ScenarioPointName.Wave1Iv => wave1?.IvEnd,
            ScenarioPointName.Wave1V => wave1?.VEnd,
            ScenarioPointName.Wave1A => wave1?.AEnd,
            ScenarioPointName.Wave1B => wave1?.BEnd,
            ScenarioPointName.Wave1C => wave1?.CEnd,
            ScenarioPointName.Wave2Origin => wave2?.Start,
            ScenarioPointName.Wave2A => wave2?.AEnd,
            ScenarioPointName.Wave2B => wave2?.BEnd,
            ScenarioPointName.Wave2C => wave2?.CEnd,
            _ => null
        };
    }

    private static ExpertScenarioRecord WithTime(ExpertScenarioRecord scenario, ScenarioPointName name, DateTimeOffset time)
    {
        ScenarioPointRecord? At(ScenarioPointRecord? point) => point == null ? null : point with { Time = time };
        var waves = scenario.Waves;
        var wave1 = scenario.Wave1;
        var wave2 = scenario.Wave2;
        return name switch
        {
            ScenarioPointName.Wave1Start => scenario with { Waves = waves! with { Wave1Start = At(waves.Wave1Start) } },
            ScenarioPointName.Wave1End => scenario with { Waves = waves! with { Wave1End = At(waves.Wave1End) } },
            ScenarioPointName.Wave2End => scenario with { Waves = waves! with { Wave2End = At(waves.Wave2End) } },
            ScenarioPointName.Wave1Origin => scenario with { Wave1 = wave1! with { Start = At(wave1.Start) } },
            ScenarioPointName.Wave1I => scenario with { Wave1 = wave1! with { IEnd = At(wave1.IEnd) } },
            ScenarioPointName.Wave1Ii => scenario with { Wave1 = wave1! with { IiEnd = At(wave1.IiEnd) } },
            ScenarioPointName.Wave1Iii => scenario with { Wave1 = wave1! with { IiiEnd = At(wave1.IiiEnd) } },
            ScenarioPointName.Wave1Iv => scenario with { Wave1 = wave1! with { IvEnd = At(wave1.IvEnd) } },
            ScenarioPointName.Wave1V => scenario with { Wave1 = wave1! with { VEnd = At(wave1.VEnd) } },
            ScenarioPointName.Wave1A => scenario with { Wave1 = wave1! with { AEnd = At(wave1.AEnd) } },
            ScenarioPointName.Wave1B => scenario with { Wave1 = wave1! with { BEnd = At(wave1.BEnd) } },
            ScenarioPointName.Wave1C => scenario with { Wave1 = wave1! with { CEnd = At(wave1.CEnd) } },
            ScenarioPointName.Wave2Origin => scenario with { Wave2 = wave2! with { Start = At(wave2.Start) } },
            ScenarioPointName.Wave2A => scenario with { Wave2 = wave2! with { AEnd = At(wave2.AEnd) } },
            ScenarioPointName.Wave2B => scenario with { Wave2 = wave2! with { BEnd = At(wave2.BEnd) } },
            ScenarioPointName.Wave2C => scenario with { Wave2 = wave2! with { CEnd = At(wave2.CEnd) } },
            _ => scenario
        };
    }

    private static ScenarioProblemRecord Misfit(string field, string message) => new ScenarioProblemRecord(field, message, Blocking: true);

    private static bool Same(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1);

    private static string Price(double price) => price.ToString("0.########", CultureInfo.InvariantCulture);

    private static string Text(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
}
