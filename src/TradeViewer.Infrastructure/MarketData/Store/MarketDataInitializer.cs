using Microsoft.Extensions.Hosting;

namespace TradeViewer.Infrastructure.MarketData.Store;

/// <summary>Opens the candle store at the start, after the folders and the workspace.</summary>
public sealed class MarketDataInitializer : IHostedService
{
    private readonly MarketDataDatabase _database;

    public MarketDataInitializer(MarketDataDatabase database)
    {
        _database = database;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _database.Initialize();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
