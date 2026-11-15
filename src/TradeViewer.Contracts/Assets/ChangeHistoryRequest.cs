namespace TradeViewer.Contracts.Assets;

/// <summary>
/// Body of PUT /api/assets/{source}/{symbol}/history: the first day (yyyy-MM-dd) per timeframe code, null for
/// the complete history. Timeframes that are not named keep their start (AS3).
/// </summary>
public sealed record ChangeHistoryRequest(IReadOnlyDictionary<string, string?> HistoryFrom);
