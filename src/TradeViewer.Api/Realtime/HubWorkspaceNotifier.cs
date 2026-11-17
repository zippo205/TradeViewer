using Microsoft.AspNetCore.SignalR;
using TradeViewer.Application.Abstractions.Realtime;

namespace TradeViewer.Api.Realtime;

/// <summary>Sends the events of the application to every connected window. A failed send is logged, never thrown.</summary>
public sealed class HubWorkspaceNotifier : IWorkspaceNotifier
{
    private readonly IHubContext<WorkspaceHub> _hub;
    private readonly ILogger<HubWorkspaceNotifier> _logger;

    public HubWorkspaceNotifier(IHubContext<WorkspaceHub> hub, ILogger<HubWorkspaceNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task CandlesUpdatedAsync(string source, string symbol, IReadOnlyList<string> timeframes)
    {
        return SendAsync("CandlesUpdated", source, symbol, timeframes.ToArray());
    }

    public Task AssetStatusChangedAsync(string source, string symbol)
    {
        return SendAsync("AssetStatusChanged", source, symbol);
    }

    public Task DownloaderStatusChangedAsync(bool reachable)
    {
        return SendAsync("DownloaderStatusChanged", reachable);
    }

    public Task DrawingsChangedAsync(string source, string symbol, string window)
    {
        return SendAsync("DrawingsChanged", source, symbol, window);
    }

    private async Task SendAsync(string name, params object[] arguments)
    {
        try
        {
            await _hub.Clients.All.SendCoreAsync(name, arguments);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The event {Event} could not be sent.", name);
        }
    }
}
