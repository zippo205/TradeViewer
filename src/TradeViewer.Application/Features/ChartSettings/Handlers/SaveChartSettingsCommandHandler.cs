using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Application.Features.ChartSettings.Validators;
using TradeViewer.Contracts.Charts;
using Settings = TradeViewer.Domain.Charts.ChartSettings;

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

        var settings = new Settings(command.Settings!.DrawingsHidden);
        if (!await _settings.SetAsync(command.Source, command.Symbol, settings, cancellationToken))
        {
            return Error.NotFound("asset.notFound", "There is no asset " + command.Source + "/" + command.Symbol + ".");
        }

        return Result.Success(command.Settings);
    }
}
