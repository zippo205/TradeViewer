namespace TradeViewer.Contracts.Workspace;

/// <summary>
/// The settings that are not part of a chart (WS5); also the body of PUT /api/settings, where a part that
/// is null stays as it is.
/// DefaultHistory: days of history per timeframe code for a new asset, null for the complete history (AS3).
/// LastChart: the chart last used (CP3), null before the first one.
/// </summary>
public sealed record SettingsResponse(IReadOnlyDictionary<string, int?>? DefaultHistory, LastChartResponse? LastChart = null);
