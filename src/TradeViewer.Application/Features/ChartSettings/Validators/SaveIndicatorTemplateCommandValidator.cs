using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Application.Features.ChartSettings.Services;
using TradeViewer.Domain.Indicators;

namespace TradeViewer.Application.Features.ChartSettings.Validators;

public sealed class SaveIndicatorTemplateCommandValidator
{
    /// <returns>Null when the template can be saved, otherwise the error naming the fields.</returns>
    public Error? Validate(SaveIndicatorTemplateCommand command, out IndicatorTemplate? template)
    {
        template = null;
        var problems = new Dictionary<string, string[]>();
        var record = command.Template;

        if (record == null)
        {
            problems["template"] = new[] { "The template is required." };
            return Invalid(problems);
        }

        if (record.Id != command.Id)
        {
            problems["id"] = new[] { "The id of the template differs from the id in the address." };
        }

        var indicators = IndicatorMapping.ToInstances(record.Indicators, "indicators", emptyAllowed: false, problems);
        var name = record.Name?.Trim() ?? "";
        var candidate = new IndicatorTemplate(command.Id, name, indicators ?? Array.Empty<IndicatorInstance>());
        foreach (var (field, messages) in IndicatorRules.Check(candidate))
        {
            if (field is "id" or "name")
            {
                problems[field] = messages;
            }
        }

        if (problems.Count > 0)
        {
            return Invalid(problems);
        }

        template = candidate;
        return null;
    }

    private static Error Invalid(IReadOnlyDictionary<string, string[]> problems)
    {
        return Error.Validation("indicatorTemplate.invalid", "The indicator template is not valid.", problems);
    }
}
