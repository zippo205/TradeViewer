using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Realtime;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Application.Features.ChartSettings.Services;
using TradeViewer.Application.Features.ChartSettings.Validators;
using TradeViewer.Contracts.Charts;
using Settings = TradeViewer.Domain.Charts.ChartSettings;

namespace TradeViewer.Application.Features.ChartSettings.Handlers;

/// <summary>
/// Saves the chart settings of an asset at once (WS1) and tells the other windows (ChartSettingsChanged), so a
/// window showing the same asset reads them again before it saves its own change.
/// </summary>
public sealed class SaveChartSettingsCommandHandler : ICommandHandler<SaveChartSettingsCommand, ChartSettingsResponse>
{
    private readonly IChartSettingsRepository _settings;
    private readonly IWorkspaceNotifier _notifier;
    private readonly SaveChartSettingsCommandValidator _validator = new SaveChartSettingsCommandValidator();

    public SaveChartSettingsCommandHandler(IChartSettingsRepository settings, IWorkspaceNotifier notifier)
    {
        _settings = settings;
        _notifier = notifier;
    }

    public async Task<Result<ChartSettingsResponse>> HandleAsync(SaveChartSettingsCommand command, CancellationToken cancellationToken)
    {
        var invalid = _validator.Validate(command, out var indicators);
        if (invalid != null)
        {
            return invalid;
        }

        // Indicators left out stay as saved: a client that does not send them cannot remove them.
        indicators ??= (await _settings.GetAsync(command.Source, command.Symbol, cancellationToken) ?? Settings.Default).Indicators;

        var settings = new Settings(command.Settings!.DrawingsHidden, indicators);
        if (!await _settings.SetAsync(command.Source, command.Symbol, settings, cancellationToken))
        {
            return Error.NotFound("asset.notFound", "There is no asset " + command.Source + "/" + command.Symbol + ".");
        }

        await _notifier.ChartSettingsChangedAsync(command.Source, command.Symbol, command.Window ?? "");
        return Result.Success(ChartSettingsMapping.ToResponse(settings));
    }
}
