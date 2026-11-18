using TradeViewer.Contracts.Indicators;

namespace TradeViewer.Contracts.Charts;

/// <summary>
/// The settings of the charts of one asset (CP2); also the body of PUT /api/chart-settings/{source}/{symbol},
/// which replaces them as a whole. DrawingsHidden: all drawings of the asset are hidden (DR6). Indicators: in
/// the order they were added (IN1); always given in an answer. In a PUT, null keeps the saved indicators, so a
/// client that does not know them yet cannot remove them (priority 1); an empty list removes them all.
/// </summary>
public sealed record ChartSettingsResponse(bool DrawingsHidden, IReadOnlyList<IndicatorRecord>? Indicators = null);
