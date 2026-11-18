using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Application.Features.ChartSettings.Services;
using TradeViewer.Domain.Indicators;

namespace TradeViewer.Application.Features.ChartSettings.Validators;

public sealed class SaveChartSettingsCommandValidator
{
    /// <param name="indicators">The indicators of the request, or null when it leaves them out.</param>
    /// <returns>Null when the settings can be saved, otherwise the error naming the fields.</returns>
    public Error? Validate(SaveChartSettingsCommand command, out IReadOnlyList<IndicatorInstance>? indicators)
    {
        indicators = null;
        var problems = new Dictionary<string, string[]>();

        if (command.Settings == null)
        {
            problems["settings"] = new[] { "The settings are required." };
        }
        else if (command.Settings.Indicators != null)
        {
            indicators = IndicatorMapping.ToInstances(command.Settings.Indicators, "indicators", emptyAllowed: true, problems);
        }

        return problems.Count == 0 ? null : Error.Validation("chartSettings.invalid", "The chart settings are not valid.", problems);
    }
}
