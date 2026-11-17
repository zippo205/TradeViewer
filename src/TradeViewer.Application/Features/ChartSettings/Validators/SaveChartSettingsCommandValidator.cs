using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.ChartSettings.Commands;

namespace TradeViewer.Application.Features.ChartSettings.Validators;

public sealed class SaveChartSettingsCommandValidator
{
    /// <returns>Null when the settings can be saved, otherwise the error naming the fields.</returns>
    public Error? Validate(SaveChartSettingsCommand command)
    {
        if (command.Settings == null)
        {
            var problems = new Dictionary<string, string[]> { ["settings"] = new[] { "The settings are required." } };
            return Error.Validation("chartSettings.invalid", "The chart settings are not valid.", problems);
        }

        return null;
    }
}
