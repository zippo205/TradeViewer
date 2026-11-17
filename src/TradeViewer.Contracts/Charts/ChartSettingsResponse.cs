namespace TradeViewer.Contracts.Charts;

/// <summary>
/// The settings of the charts of one asset (CP2); also the body of PUT /api/chart-settings/{source}/{symbol},
/// which replaces them as a whole. DrawingsHidden: all drawings of the asset are hidden (DR6).
/// </summary>
public sealed record ChartSettingsResponse(bool DrawingsHidden);
