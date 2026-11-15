using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Application.Features.ChartSettings.Validators;
using TradeViewer.Contracts.Charts;

namespace TradeViewer.Application.Features.ChartSettings.Handlers;

public sealed class SaveChartSettingsCommandHandler : ICommandHandler<SaveChartSettingsCommand, ChartSettingsResponse>
{
    private readonly IChartSettingsRepository _settings;
    private readonly SaveChartSettingsCommandValidator _validator = new SaveChartSettingsCommandValidator();

    public SaveChartSettingsCommandHandler(IChartSettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<Result<ChartSettingsResponse>> HandleAsync(SaveChartSettingsCommand command, CancellationToken cancellationToken)
    {
        var invalid = _validator.Validate(command);
        if (invalid != null)
        {
            return invalid;
        }

        if (!await _settings.SetAsync(command.Source, command.Symbol, command.Settings, cancellationToken))
        {
            return Error.NotFound("asset.notFound", "There is no asset " + command.Source + "/" + command.Symbol + ".");
        }

        return Result.Success(command.Settings);
    }
}
