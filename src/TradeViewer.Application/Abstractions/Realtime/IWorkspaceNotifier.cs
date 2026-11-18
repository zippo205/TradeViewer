namespace TradeViewer.Application.Abstractions.Realtime;

/// <summary>
/// Tells the open windows what changed (hub /hubs/workspace). An event carries no data: the client reads
/// what it shows again through the API. Sending never fails the caller.
/// </summary>
public interface IWorkspaceNotifier
{
    /// <summary>Candles of these timeframes (codes such as 15m) of the asset were added, changed or removed (MD2).</summary>
    Task CandlesUpdatedAsync(string source, string symbol, IReadOnlyList<string> timeframes);

    /// <summary>The state of the asset changed: added, removed, loading progress, a warning (AS4).</summary>
    Task AssetStatusChangedAsync(string source, string symbol);

    Task DownloaderStatusChangedAsync(bool reachable);

    /// <summary>
    /// A drawing of the asset was saved or deleted. Window is the id of the window that made the change (empty
    /// when unknown): it ignores the event, every other window showing the asset reads its drawings again.
    /// </summary>
    Task DrawingsChangedAsync(string source, string symbol, string window);

    /// <summary>
    /// The chart settings of the asset were saved (CP2). Window is the id of the window that saved them (empty when
    /// unknown): it ignores the event, every other window showing the asset reads the settings again.
    /// </summary>
    Task ChartSettingsChangedAsync(string source, string symbol, string window);
}
