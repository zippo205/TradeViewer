using TradeViewer.Domain.Charts;

namespace TradeViewer.Application.Abstractions.Workspace;

/// <summary>The chart settings per asset in workspace.db (CP2), on the table of script 0003. A write is saved at once (WS1).</summary>
public interface IChartSettingsRepository
{
    /// <returns>The saved settings, or null when the user never changed them.</returns>
    Task<ChartSettings?> GetAsync(string source, string symbol, CancellationToken cancellationToken);

    /// <summary>Replaces the settings of the asset as a whole.</summary>
    /// <returns>False when there is no such asset.</returns>
    Task<bool> SetAsync(string source, string symbol, ChartSettings settings, CancellationToken cancellationToken);
}
