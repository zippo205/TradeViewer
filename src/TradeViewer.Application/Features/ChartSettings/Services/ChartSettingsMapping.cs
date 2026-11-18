using TradeViewer.Contracts.Charts;
using Settings = TradeViewer.Domain.Charts.ChartSettings;

namespace TradeViewer.Application.Features.ChartSettings.Services;

public static class ChartSettingsMapping
{
    public static ChartSettingsResponse ToResponse(Settings settings)
    {
        return new ChartSettingsResponse(settings.DrawingsHidden, settings.Indicators.Select(IndicatorMapping.ToRecord).ToList());
    }
}
