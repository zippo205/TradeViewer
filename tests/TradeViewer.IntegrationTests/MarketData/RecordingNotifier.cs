using TradeViewer.Application.Abstractions.Realtime;

namespace TradeViewer.IntegrationTests.MarketData;

/// <summary>Keeps the events instead of sending them, e.g. "CandlesUpdated binance/BTCUSDT 15m,1d".</summary>
public sealed class RecordingNotifier : IWorkspaceNotifier
{
    private readonly List<string> _events = new List<string>();

    public IReadOnlyList<string> Events
    {
        get
        {
            lock (_events)
            {
                return _events.ToList();
            }
        }
    }

    public Task CandlesUpdatedAsync(string source, string symbol, IReadOnlyList<string> timeframes)
    {
        return Add("CandlesUpdated " + source + "/" + symbol + " " + string.Join(",", timeframes));
    }

    public Task AssetStatusChangedAsync(string source, string symbol)
    {
        return Add("AssetStatusChanged " + source + "/" + symbol);
    }

    public Task DownloaderStatusChangedAsync(bool reachable)
    {
        return Add("DownloaderStatusChanged " + reachable);
    }

    public Task DrawingsChangedAsync(string source, string symbol, string window)
    {
        return Add("DrawingsChanged " + source + "/" + symbol + " " + window);
    }

    private Task Add(string text)
    {
        lock (_events)
        {
            _events.Add(text);
        }

        return Task.CompletedTask;
    }
}
