namespace TradeViewer.Web.Drawings;

/// <summary>
/// The id of this browser window. Requests that change drawings carry it (X-TradeViewer-Window), and the hub
/// event DrawingsChanged brings it back, so the window that made a change ignores its own event.
/// </summary>
public sealed class WindowIdentity
{
    public const string Header = "X-TradeViewer-Window";

    public string Id { get; } = Guid.NewGuid().ToString("N");
}
