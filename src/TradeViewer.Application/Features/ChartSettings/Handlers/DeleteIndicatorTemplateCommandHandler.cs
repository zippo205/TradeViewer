using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.ChartSettings.Commands;

namespace TradeViewer.Application.Features.ChartSettings.Handlers;

public sealed class DeleteIndicatorTemplateCommandHandler : ICommandHandler<DeleteIndicatorTemplateCommand, Unit>
{
    private readonly IIndicatorTemplateRepository _templates;

    public DeleteIndicatorTemplateCommandHandler(IIndicatorTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task<Result<Unit>> HandleAsync(DeleteIndicatorTemplateCommand command, CancellationToken cancellationToken)
    {
        if (!await _templates.DeleteAsync(command.Id, cancellationToken))
        {
            return Error.NotFound("indicatorTemplate.notFound", "There is no indicator template " + command.Id + ".");
        }

        return Result.Success();
    }
}
