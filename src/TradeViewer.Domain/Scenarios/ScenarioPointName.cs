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

    Wave1I,
    Wave1Ii,
    Wave1Iii,
    Wave1Iv,
    Wave1V,
    Wave1A,
    Wave1B,
    Wave1C,

    Wave2A,
    Wave2B,
    Wave2C,

    Entry,
    Add,
    Stop,
    StopMove,
    Exit
}
