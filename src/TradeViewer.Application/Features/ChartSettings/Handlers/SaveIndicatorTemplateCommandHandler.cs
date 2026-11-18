using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Application.Features.ChartSettings.Services;
using TradeViewer.Application.Features.ChartSettings.Validators;
using TradeViewer.Contracts.Indicators;

namespace TradeViewer.Application.Features.ChartSettings.Handlers;

/// <summary>Stores a template (IN2). Two templates cannot have the same name, whatever the case of its letters.</summary>
public sealed class SaveIndicatorTemplateCommandHandler : ICommandHandler<SaveIndicatorTemplateCommand, IndicatorTemplateRecord>
{
    private readonly IIndicatorTemplateRepository _templates;
    private readonly SaveIndicatorTemplateCommandValidator _validator = new SaveIndicatorTemplateCommandValidator();

    public SaveIndicatorTemplateCommandHandler(IIndicatorTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task<Result<IndicatorTemplateRecord>> HandleAsync(SaveIndicatorTemplateCommand command, CancellationToken cancellationToken)
    {
        var invalid = _validator.Validate(command, out var template);
        if (invalid != null)
        {
            return invalid;
        }

        var all = await _templates.GetAllAsync(cancellationToken);
        if (all.Any(other => other.Id != template!.Id && string.Equals(other.Name, template.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("indicatorTemplate.nameTaken", "There is already a template named " + template!.Name + ".");
        }

        await _templates.SaveAsync(template!, cancellationToken);
        return Result.Success(IndicatorMapping.ToRecord(template!));
    }
}
