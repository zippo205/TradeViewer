namespace TradeViewer.Domain.Scenarios;

/// <summary>
/// What each named point is: its level (SC11), how it snaps (SC10) and its field in the file. Every expert scenario
/// is long (SC9): wave 1 rises from a low to a high, wave 2 falls to a low; the internals of wave 1 end alternately
/// at highs and lows from i (or a) on, those of wave 2 at lows and highs from a on.
/// </summary>
public static class ScenarioPoints
{
    /// <summary>The points of wave 1 for a five (impulse, leading diagonal), in their order.</summary>
    public static IReadOnlyList<ScenarioPointName> Wave1Five { get; } = new[]
    {
        ScenarioPointName.Wave1I, ScenarioPointName.Wave1Ii, ScenarioPointName.Wave1Iii, ScenarioPointName.Wave1Iv, ScenarioPointName.Wave1V
    };

    /// <summary>The points of wave 1 for a three, in their order.</summary>
    public static IReadOnlyList<ScenarioPointName> Wave1Three { get; } = new[] { ScenarioPointName.Wave1A, ScenarioPointName.Wave1B, ScenarioPointName.Wave1C };

    /// <summary>
    /// The points of a nested wave that are the same turning point as a point of the context, on the setup's timeframe
    /// (decision 0013): the start of wave 1, the end of a five or a three of wave 1, the start and the end of wave 2.
    /// </summary>
    public static IReadOnlyList<(ScenarioPointName Nested, ScenarioPointName Context)> Turnings { get; } = new[]
    {
        (ScenarioPointName.Wave1Origin, ScenarioPointName.Wave1Start),
        (ScenarioPointName.Wave1V, ScenarioPointName.Wave1End),
        (ScenarioPointName.Wave1C, ScenarioPointName.Wave1End),
        (ScenarioPointName.Wave2Origin, ScenarioPointName.Wave1End),
        (ScenarioPointName.Wave2C, ScenarioPointName.Wave2End)
    };

    /// <summary>The points of wave 2, in their order.</summary>
    public static IReadOnlyList<ScenarioPointName> Wave2Points { get; } = new[] { ScenarioPointName.Wave2A, ScenarioPointName.Wave2B, ScenarioPointName.Wave2C };

    public static ScenarioLevel LevelOf(ScenarioPointName name)
    {
        switch (name)
        {
            case ScenarioPointName.Wave1Start:
            case ScenarioPointName.Wave1End:
            case ScenarioPointName.Wave2End:
                return ScenarioLevel.Context;
            case ScenarioPointName.Entry:
            case ScenarioPointName.Add:
            case ScenarioPointName.Stop:
            case ScenarioPointName.StopMove:
            case ScenarioPointName.Exit:
                return ScenarioLevel.Trigger;
            default:
                return ScenarioLevel.Setup;
        }
    }

    public static ScenarioSnapKind SnapOf(ScenarioPointName name)
    {
        switch (name)
        {
            case ScenarioPointName.Wave1Start:
            case ScenarioPointName.Wave1Origin:
            case ScenarioPointName.Wave2End:
            case ScenarioPointName.Wave1Ii:
            case ScenarioPointName.Wave1Iv:
            case ScenarioPointName.Wave1B:
            case ScenarioPointName.Wave2A:
            case ScenarioPointName.Wave2C:
                return ScenarioSnapKind.Low;
            case ScenarioPointName.Wave1End:
            case ScenarioPointName.Wave2Origin:
            case ScenarioPointName.Wave1I:
            case ScenarioPointName.Wave1Iii:
            case ScenarioPointName.Wave1V:
            case ScenarioPointName.Wave1A:
            case ScenarioPointName.Wave1C:
            case ScenarioPointName.Wave2B:
                return ScenarioSnapKind.High;
            case ScenarioPointName.Entry:
            case ScenarioPointName.Add:
                return ScenarioSnapKind.Close;
            case ScenarioPointName.StopMove:
            case ScenarioPointName.Exit:
                return ScenarioSnapKind.GivenPrice;
            case ScenarioPointName.Stop:
                return ScenarioSnapKind.PriceOnly;
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }
    }

    /// <summary>The field of the point in the file, e.g. waves.wave1Start or wave1.iEnd; stop moves and exits add their index.</summary>
    public static string FieldOf(ScenarioPointName name, int index = 0)
    {
        switch (name)
        {
            case ScenarioPointName.Wave1Start:
                return "waves.wave1Start";
            case ScenarioPointName.Wave1End:
                return "waves.wave1End";
            case ScenarioPointName.Wave2End:
                return "waves.wave2End";
            case ScenarioPointName.Wave1Origin:
                return "wave1.start";
            case ScenarioPointName.Wave2Origin:
                return "wave2.start";
            case ScenarioPointName.Wave1I:
                return "wave1.iEnd";
            case ScenarioPointName.Wave1Ii:
                return "wave1.iiEnd";
            case ScenarioPointName.Wave1Iii:
                return "wave1.iiiEnd";
            case ScenarioPointName.Wave1Iv:
                return "wave1.ivEnd";
            case ScenarioPointName.Wave1V:
                return "wave1.vEnd";
            case ScenarioPointName.Wave1A:
                return "wave1.aEnd";
            case ScenarioPointName.Wave1B:
                return "wave1.bEnd";
            case ScenarioPointName.Wave1C:
                return "wave1.cEnd";
            case ScenarioPointName.Wave2A:
                return "wave2.aEnd";
            case ScenarioPointName.Wave2B:
                return "wave2.bEnd";
            case ScenarioPointName.Wave2C:
                return "wave2.cEnd";
            case ScenarioPointName.Entry:
                return "trade.entry";
            case ScenarioPointName.Add:
                return "trade.add";
            case ScenarioPointName.Stop:
                return "trade.stop";
            case ScenarioPointName.StopMove:
                return "trade.stopMoves[" + index + "]";
            case ScenarioPointName.Exit:
                return "trade.exits[" + index + "]";
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }
    }
}
