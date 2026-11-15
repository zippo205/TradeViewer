namespace TradeViewer.Contracts.Workspace;

/// <summary>
/// The settings that are not part of a chart (WS5); also the body of PUT /api/settings.
/// DefaultHistory: days of history per timeframe code for a new asset, null for the complete history (AS3).
/// </summary>
public sealed record SettingsResponse(IReadOnlyDictionary<string, int?> DefaultHistory);
