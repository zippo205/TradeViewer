namespace TradeViewer.Contracts.Charts;

/// <summary>
/// The settings of the chart of one asset (CP2); also the body of PUT /api/chart-settings/{source}/{symbol},
/// which replaces them as a whole. PriceScale: linear or log. Later epics add their settings here.
/// </summary>
public sealed record ChartSettingsResponse(string PriceScale)
{
    public const string Linear = "linear";

    public const string Log = "log";

    /// <summary>What a chart shows before the user changed anything.</summary>
    public static ChartSettingsResponse Default { get; } = new ChartSettingsResponse(Linear);
}
