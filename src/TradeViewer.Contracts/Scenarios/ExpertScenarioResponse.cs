using TradeViewer.Contracts.Overlays;

namespace TradeViewer.Contracts.Scenarios;

/// <summary>
/// An expert scenario as the editor gets it after every read, snap, preview and save: the scenario, the shapes built
/// from its named points (SC8), its form problems at their fields (SC15), the timeframes of its three levels (SC11)
/// and the state of its asset (SC6).
/// </summary>
public sealed record ExpertScenarioResponse(
    ExpertScenarioRecord Scenario,
    IReadOnlyList<OverlayGroupRecord> Groups,
    IReadOnlyList<ScenarioProblemRecord> Problems,
    IReadOnlyList<ScenarioLevelRecord> Levels,
    ScenarioAssetRecord Asset);
