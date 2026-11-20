using TradeViewer.Contracts.Scenarios;
using TradeViewer.Domain.MarketData;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Services;

/// <summary>
/// Puts a snapped point into the scenario (SC10). A section that does not exist yet is created with the timeframe of
/// its level; wave 1 gets its pattern from the first point (impulse for i to v, three for a to c). A moved point keeps
/// its reason. A new exit gets the share that is left to 100 % and the kind of its tool.
/// </summary>
public static class ScenarioPointEditor
{
    private const string StopMoves = "trade.stopMoves";
    private const string Exits = "trade.exits";

    /// <summary>The point a field of a snap request names, or null for a field that is no point.</summary>
    public static ScenarioPointName? NameOf(string? field)
    {
        if (field == StopMoves)
        {
            return ScenarioPointName.StopMove;
        }

        if (field == Exits)
        {
            return ScenarioPointName.Exit;
        }

        foreach (var name in Enum.GetValues<ScenarioPointName>())
        {
            if (name != ScenarioPointName.StopMove && name != ScenarioPointName.Exit && ScenarioPoints.FieldOf(name) == field)
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// The timeframe the point snaps on: the one its section names (a file may have been written by hand with others,
    /// decision 0012), else the timeframe of its level.
    /// </summary>
    public static Timeframe? SnapTimeframe(ExpertScenarioRecord scenario, ScenarioPointName name)
    {
        if (Timeframes.TryParse(SectionTimeframe(scenario, name), out var section))
        {
            return section;
        }

        if (!Timeframes.TryParse(scenario.ContextTimeframe, out var context) || !ScenarioLevels.IsContext(context))
        {
            return null;
        }

        return ScenarioLevels.TimeframeOf(context, ScenarioPoints.LevelOf(name));
    }

    public static ExpertScenarioRecord Place(ExpertScenarioRecord scenario, ScenarioPointName name, int? index, string? kind, ScenarioPoint point, Timeframe timeframe)
    {
        var code = Timeframes.Code(timeframe);
        var time = DateTimeOffset.FromUnixTimeSeconds(point.Time);
        ScenarioPointRecord Moved(ScenarioPointRecord? old) => new ScenarioPointRecord(time, point.Price, old?.Why);

        switch (name)
        {
            case ScenarioPointName.Wave1Start:
            case ScenarioPointName.Wave1End:
            case ScenarioPointName.Wave2End:
                {
                    var waves = scenario.Waves ?? new ScenarioWavesRecord(code, null, null, null);
                    waves = name == ScenarioPointName.Wave1Start ? waves with { Wave1Start = Moved(waves.Wave1Start) }
                        : name == ScenarioPointName.Wave1End ? waves with { Wave1End = Moved(waves.Wave1End) }
                        : waves with { Wave2End = Moved(waves.Wave2End) };
                    return scenario with { Waves = waves with { Timeframe = waves.Timeframe ?? code } };
                }

            case ScenarioPointName.Wave2A:
            case ScenarioPointName.Wave2B:
            case ScenarioPointName.Wave2C:
                {
                    var wave2 = scenario.Wave2 ?? new ScenarioWave2Record(code, null);
                    wave2 = name == ScenarioPointName.Wave2A ? wave2 with { AEnd = Moved(wave2.AEnd) }
                        : name == ScenarioPointName.Wave2B ? wave2 with { BEnd = Moved(wave2.BEnd) }
                        : wave2 with { CEnd = Moved(wave2.CEnd) };
                    return scenario with { Wave2 = wave2 with { Timeframe = wave2.Timeframe ?? code } };
                }

            case ScenarioPointName.Entry:
                {
                    var trade = TradeOf(scenario, code);
                    return scenario with { Trade = trade with { Entry = Moved(trade.Entry) } };
                }

            case ScenarioPointName.Add:
                {
                    var trade = TradeOf(scenario, code);
                    return scenario with { Trade = trade with { Add = Moved(trade.Add) } };
                }

            case ScenarioPointName.Stop:
                {
                    var trade = TradeOf(scenario, code);
                    return scenario with { Trade = trade with { Stop = new ScenarioPriceRecord(point.Price, trade.Stop?.Why) } };
                }

            case ScenarioPointName.StopMove:
                {
                    var trade = TradeOf(scenario, code);
                    var moves = (trade.StopMoves ?? Array.Empty<ScenarioPointRecord>()).ToList();
                    var at = Position(index, moves.Count);
                    if (at < moves.Count)
                    {
                        moves[at] = Moved(moves[at]);
                    }
                    else
                    {
                        moves.Add(Moved(null));
                    }

                    return scenario with { Trade = trade with { StopMoves = moves } };
                }

            case ScenarioPointName.Exit:
                {
                    var trade = TradeOf(scenario, code);
                    var exits = (trade.Exits ?? Array.Empty<ScenarioExitRecord>()).ToList();
                    var at = Position(index, exits.Count);
                    if (at < exits.Count)
                    {
                        exits[at] = exits[at] with { Time = time, Price = point.Price };
                    }
                    else
                    {
                        var left = Math.Max(0, 100 - exits.Sum(exit => exit.Share));
                        exits.Add(new ScenarioExitRecord(time, point.Price, left, kind ?? ScenarioCodes.Target));
                    }

                    return scenario with { Trade = trade with { Exits = exits } };
                }

            default:
                return PlaceInWave1(scenario, name, Moved, code);
        }
    }

    private static ExpertScenarioRecord PlaceInWave1(ExpertScenarioRecord scenario, ScenarioPointName name, Func<ScenarioPointRecord?, ScenarioPointRecord> moved, string code)
    {
        var pattern = ScenarioPoints.Wave1Three.Contains(name) ? ScenarioCodes.Three : ScenarioCodes.Impulse;
        var wave1 = scenario.Wave1 ?? new ScenarioWave1Record(code, pattern);
        wave1 = name switch
        {
            ScenarioPointName.Wave1I => wave1 with { IEnd = moved(wave1.IEnd) },
            ScenarioPointName.Wave1Ii => wave1 with { IiEnd = moved(wave1.IiEnd) },
            ScenarioPointName.Wave1Iii => wave1 with { IiiEnd = moved(wave1.IiiEnd) },
            ScenarioPointName.Wave1Iv => wave1 with { IvEnd = moved(wave1.IvEnd) },
            ScenarioPointName.Wave1V => wave1 with { VEnd = moved(wave1.VEnd) },
            ScenarioPointName.Wave1A => wave1 with { AEnd = moved(wave1.AEnd) },
            ScenarioPointName.Wave1B => wave1 with { BEnd = moved(wave1.BEnd) },
            ScenarioPointName.Wave1C => wave1 with { CEnd = moved(wave1.CEnd) },
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        return scenario with { Wave1 = wave1 with { Timeframe = wave1.Timeframe ?? code, Pattern = wave1.Pattern ?? pattern } };
    }

    private static ScenarioTradeRecord TradeOf(ExpertScenarioRecord scenario, string code)
    {
        var trade = scenario.Trade ?? new ScenarioTradeRecord(code, null, null, null, null, null);
        return trade with { Timeframe = trade.Timeframe ?? code };
    }

    private static int Position(int? index, int count)
    {
        return index is { } at && at >= 0 && at < count ? at : count;
    }

    private static string? SectionTimeframe(ExpertScenarioRecord scenario, ScenarioPointName name)
    {
        switch (ScenarioPoints.LevelOf(name))
        {
            case ScenarioLevel.Context:
                return scenario.Waves?.Timeframe;
            case ScenarioLevel.Trigger:
                return scenario.Trade?.Timeframe;
            default:
                return ScenarioPoints.Wave2Points.Contains(name) ? scenario.Wave2?.Timeframe : scenario.Wave1?.Timeframe;
        }
    }
}
