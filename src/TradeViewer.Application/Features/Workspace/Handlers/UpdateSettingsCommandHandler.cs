using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Workspace.Commands;
using TradeViewer.Application.Features.Workspace.Services;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Handlers;

public sealed class UpdateSettingsCommandHandler : ICommandHandler<UpdateSettingsCommand, SettingsResponse>
{
    private readonly IHistorySettingsRepository _history;
    private readonly ISettingsRepository _settings;

    public UpdateSettingsCommandHandler(IHistorySettingsRepository history, ISettingsRepository settings)
    {
        _history = history;
        _settings = settings;
    }

    public async Task<Result<SettingsResponse>> HandleAsync(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        if (command.DefaultHistory == null && command.LastChart == null && command.PriceScale == null && command.Magnet == null)
        {
            return Error.Validation(
                "settings.invalid",
                "The settings are not valid.",
                new Dictionary<string, string[]> { ["settings"] = new[] { "The default history, the last chart, the price scale or the magnet is required." } });
        }

        // Everything is checked before anything is written.
        var history = command.DefaultHistory == null ? null : SettingsMapping.ToHistory(command.DefaultHistory);
        if (history != null && history.IsFailure)
        {
            return history.Error;
        }

        var invalidChart = command.LastChart == null ? null : LastChartSetting.Validate(command.LastChart);
        if (invalidChart != null)
        {
            return invalidChart;
        }

        var invalidScale = command.PriceScale == null ? null : PriceScaleSetting.Validate(command.PriceScale);
        if (invalidScale != null)
        {
            return invalidScale;
        }

        if (history != null)
        {
            await _history.SetAsync(history.Value, cancellationToken);
        }

        if (command.PriceScale != null)
        {
            await PriceScaleSetting.WriteAsync(_settings, command.PriceScale, cancellationToken);
        }

        if (command.Magnet != null)
        {
            await MagnetSetting.WriteAsync(_settings, command.Magnet.Value, cancellationToken);
        }

        if (command.LastChart != null)
        {
            await LastChartSetting.WriteAsync(_settings, command.LastChart, cancellationToken);
        }

        return Result.Success(SettingsMapping.ToResponse(
            await _history.GetAsync(cancellationToken),
            await LastChartSetting.ReadAsync(_settings, cancellationToken),
            await PriceScaleSetting.ReadAsync(_settings, cancellationToken),
            await MagnetSetting.ReadAsync(_settings, cancellationToken)));
    }
}
