namespace TradeViewer.Domain.Scenarios;

/// <summary>The codes of EWTS's expert format: verdicts, patterns, exit kinds and the one direction.</summary>
public static class ScenarioCodes
{
    public const string Long = "long";

    public const string TradeWin = "trade-win";
    public const string TradeLoss = "trade-loss";
    public const string NoTrade = "no-trade";

    public const string Target = "target";
    public const string StopKind = "stop";

    public const string Impulse = "impulse";
    public const string LeadingDiagonal = "leading-diagonal";
    public const string Three = "three";

    public const string Zigzag = "zigzag";
    public const string Flat = "flat";
    public const string ExpandedFlat = "expanded-flat";
    public const string RunningFlat = "running-flat";
    public const string Triangle = "triangle";
    public const string Complex = "complex";

    public static IReadOnlyList<string> Verdicts { get; } = new[] { TradeWin, TradeLoss, NoTrade };

    public static IReadOnlyList<string> ExitKinds { get; } = new[] { Target, StopKind };

    public static IReadOnlyList<string> Wave1Patterns { get; } = new[] { Impulse, LeadingDiagonal, Three };

    public static IReadOnlyList<string> Wave2Patterns { get; } = new[] { Zigzag, Flat, ExpandedFlat, RunningFlat, Triangle, Complex };

    /// <summary>The patterns of wave 2 that need the ends of a and b.</summary>
    public static IReadOnlyList<string> Wave2PatternsWithAb { get; } = new[] { Zigzag, Flat, ExpandedFlat, RunningFlat };

    public static bool IsTrade(string? verdict)
    {
        return verdict == TradeWin || verdict == TradeLoss;
    }
}
