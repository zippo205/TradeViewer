using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Contracts.Charts;

namespace TradeViewer.Application.Features.ChartSettings.Validators;

public sealed class SaveChartSettingsCommandValidator
{
    /// <returns>Null when the settings are valid, otherwise the error naming the fields.</returns>
    public Error? Validate(SaveChartSettingsCommand command)
    {
        var scale = command.Settings?.PriceScale;
        if (scale == ChartSettingsResponse.Linear || scale == ChartSettingsResponse.Log)
        {
            return null;
        }

        return Error.Validation(
            "chartSettings.invalid",
            "The chart settings are not valid.",
            new Dictionary<string, string[]> { ["priceScale"] = new[] { "Must be linear or log." } });
    }
}
