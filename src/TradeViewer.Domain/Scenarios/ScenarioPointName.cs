namespace TradeViewer.Domain.Scenarios;

/// <summary>
/// The named points of an expert scenario (docs/knowledge/ewts-interface.md, "Expert Scenario Files"). Stop, StopMove
/// and Exit can occur several times or have no time; the others are one point each. <see cref="ScenarioPoints"/>
/// knows each one's level, field and snap rule.
/// </summary>
public enum ScenarioPointName
{
    Wave1Start,
    Wave1End,
    Wave2End,

    /// <summary>The start of wave 1 on the setup's timeframe (wave1.start, decision 0013); the same turning point as Wave1Start.</summary>
    Wave1Origin,
    Wave1I,
    Wave1Ii,
    Wave1Iii,
    Wave1Iv,
    Wave1V,
    Wave1A,
    Wave1B,
    Wave1C,

    /// <summary>The start of wave 2 on the setup's timeframe (wave2.start, decision 0013); the same turning point as Wave1End.</summary>
    Wave2Origin,
    Wave2A,
    Wave2B,
    Wave2C,

    Entry,
    Add,
    Stop,
    StopMove,
    Exit
}
