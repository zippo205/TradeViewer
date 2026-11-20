namespace TradeViewer.Contracts.Scenarios;

/// <summary>
/// The asset of a scenario in TradeViewer (SC6). InWorkspace false: the page offers to add it. HistoryNeeded: the
/// history starts (yyyy-MM-dd per timeframe code) the asset needs so the time span of the scenario is stored on all
/// three levels; empty when it is. The page sends them, after the user agreed, as a history change of the asset.
/// </summary>
public sealed record ScenarioAssetRecord(bool InWorkspace, IReadOnlyDictionary<string, string?> HistoryNeeded);
