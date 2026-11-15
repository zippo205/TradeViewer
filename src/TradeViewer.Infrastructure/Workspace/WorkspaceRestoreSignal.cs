namespace TradeViewer.Infrastructure.Workspace;

/// <summary>
/// Raised after workspace.db was replaced by a backup. Whoever keeps something in line with the assets (the
/// subscriptions at the Downloader, the candle store) listens and reconciles (AS7).
/// </summary>
public sealed class WorkspaceRestoreSignal
{
    public event Action? Restored;

    public void Raise()
    {
        Restored?.Invoke();
    }
}
