namespace TradeViewer.Contracts.Workspace;

/// <summary>
/// The settings that are not part of a chart (WS5); also the body of PUT /api/settings, where a part that
/// is null stays as it is.
/// DefaultHistory: days of history per timeframe code for a new asset, null for the complete history (AS3).
/// LastChart: the chart last used (CP3), null before the first one.
/// PriceScale: the price scale mode of all charts, linear or log (CP2).
/// </summary>
public sealed record SettingsResponse(
    IReadOnlyDictionary<string, int?>? DefaultHistory,
    LastChartResponse? LastChart = null,
    string? PriceScale = null);

/// <summary>The price scale modes of a chart (CH1).</summary>
public static class PriceScales
{
    public const string Linear = "linear";

    public const string Log = "log";
}
