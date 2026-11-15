using Microsoft.AspNetCore.SignalR;

namespace TradeViewer.Api.Realtime;

/// <summary>
/// /hubs/workspace: events from the server to the open windows only; what the user does goes through the
/// API (architecture, chapter 9).
/// </summary>
public sealed class WorkspaceHub : Hub
{
    public const string Route = "/hubs/workspace";
}
