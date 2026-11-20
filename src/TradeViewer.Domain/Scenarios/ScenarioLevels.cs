using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Scenarios;

/// <summary>
/// The timeframes of the three levels, from the chain of the context timeframe as EWTS uses it (SC11): context 1d,
/// setup 4h, trigger 1h; context 4h, setup 1h, trigger 15m. The codes are those of the file's drawings section.
/// </summary>
public static class ScenarioLevels
{
    public static IReadOnlyList<ScenarioLevel> All { get; } = new[] { ScenarioLevel.Context, ScenarioLevel.Setup, ScenarioLevel.Trigger };

    /// <summary>Whether a scenario can have the timeframe as its context: 1d or 4h.</summary>
    public static bool IsContext(Timeframe timeframe)
    {
        return timeframe == Timeframe.D1 || timeframe == Timeframe.H4;
    }

    /// <summary>The timeframe of a level of a scenario with that context timeframe.</summary>
    public static Timeframe TimeframeOf(Timeframe context, ScenarioLevel level)
    {
        if (!IsContext(context))
        {
            throw new ArgumentOutOfRangeException(nameof(context), context, "An expert scenario has the context 1d or 4h.");
        }

        switch (level)
        {
            case ScenarioLevel.Context:
                return context;
            case ScenarioLevel.Setup:
                return context == Timeframe.D1 ? Timeframe.H4 : Timeframe.H1;
            case ScenarioLevel.Trigger:
                return context == Timeframe.D1 ? Timeframe.H1 : Timeframe.M15;
            default:
                throw new ArgumentOutOfRangeException(nameof(level));
        }
    }

    /// <summary>The level whose timeframe it is, or null for a timeframe outside the three.</summary>
    public static ScenarioLevel? LevelOf(Timeframe context, Timeframe timeframe)
    {
        foreach (var level in All)
        {
            if (TimeframeOf(context, level) == timeframe)
            {
                return level;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether what belongs to a level is shown on the timeframe of another (SC21): on its own and on every lower
    /// level, never on a higher one.
    /// </summary>
    public static bool IsShownOn(ScenarioLevel owner, ScenarioLevel shown)
    {
        return shown >= owner;
    }

    public static string Code(ScenarioLevel level)
    {
        switch (level)
        {
            case ScenarioLevel.Context:
                return "context";
            case ScenarioLevel.Setup:
                return "setup";
            case ScenarioLevel.Trigger:
                return "trigger";
            default:
                throw new ArgumentOutOfRangeException(nameof(level));
        }
    }

    public static bool TryParse(string? code, out ScenarioLevel level)
    {
        foreach (var candidate in All)
        {
            if (Code(candidate) == code)
            {
                level = candidate;
                return true;
            }
        }

        level = default;
        return false;
    }
}
