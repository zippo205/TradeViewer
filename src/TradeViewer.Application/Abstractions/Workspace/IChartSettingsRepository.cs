using TradeViewer.Contracts.Charts;

namespace TradeViewer.Application.Abstractions.Workspace;

/// <summary>The chart settings per asset in workspace.db (CP2). A write is saved at once (WS1).</summary>
public interface IChartSettingsRepository
{
    /// <returns>The saved settings, or null when the user never changed them.</returns>
    Task<ChartSettingsResponse?> GetAsync(string source, string symbol, CancellationToken cancellationToken);

    /// <returns>False when there is no such asset.</returns>
    Task<bool> SetAsync(string source, string symbol, ChartSettingsResponse settings, CancellationToken cancellationToken);
}
