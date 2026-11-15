using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace TradeViewer.Web.Realtime;

/// <summary>
/// The client of the hub /hubs/workspace. An event says what changed; the page then reads the data again
/// through the API. After a reconnect <see cref="Reconnected"/> tells the pages to reload what they show.
/// </summary>
public sealed class WorkspaceEvents : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private Task? _start;

    public WorkspaceEvents(NavigationManager navigation)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(navigation.ToAbsoluteUri("hubs/workspace"))
            .WithAutomaticReconnect(new RetryForever())
            .Build();

        _connection.On<string, string>("AssetStatusChanged", (source, symbol) => AssetStatusChanged?.Invoke(source, symbol));
        _connection.On<string, string, string[]>("CandlesUpdated", (source, symbol, timeframes) => CandlesUpdated?.Invoke(source, symbol, timeframes));
        _connection.On<bool>("DownloaderStatusChanged", reachable => DownloaderStatusChanged?.Invoke(reachable));
        _connection.Reconnected += _ =>
        {
            Reconnected?.Invoke();
            return Task.CompletedTask;
        };
    }

    public event Action<string, string>? AssetStatusChanged;

    public event Action<string, string, string[]>? CandlesUpdated;

    public event Action<bool>? DownloaderStatusChanged;

    public event Action? Reconnected;

    /// <summary>Connects once; every page that listens calls it. A server that is down is tried again in the background.</summary>
    public Task StartAsync()
    {
        _start ??= ConnectAsync();
        return _start;
    }

    public ValueTask DisposeAsync()
    {
        return _connection.DisposeAsync();
    }

    private async Task ConnectAsync()
    {
        while (true)
        {
            try
            {
                await _connection.StartAsync();
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TimeoutException || ex is IOException)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }

    /// <summary>Tries again after 0, 2, 5 and then every 10 seconds, without giving up.</summary>
    private sealed class RetryForever : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            switch (retryContext.PreviousRetryCount)
            {
                case 0:
                    return TimeSpan.Zero;
                case 1:
                    return TimeSpan.FromSeconds(2);
                case 2:
                    return TimeSpan.FromSeconds(5);
                default:
                    return TimeSpan.FromSeconds(10);
            }
        }
    }
}
