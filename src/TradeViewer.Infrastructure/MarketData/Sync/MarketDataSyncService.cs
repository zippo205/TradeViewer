using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeViewer.Infrastructure.Configuration;

namespace TradeViewer.Infrastructure.MarketData.Sync;

/// <summary>Runs the sync in the background. With MarketData:Downloader:Enabled false it does nothing.</summary>
public sealed class MarketDataSyncService : BackgroundService
{
    private static readonly TimeSpan AfterAnError = TimeSpan.FromSeconds(30);

    private readonly MarketDataSync _sync;
    private readonly TimeProvider _time;
    private readonly bool _enabled;
    private readonly ILogger<MarketDataSyncService> _logger;

    public MarketDataSyncService(MarketDataSync sync, IOptions<MarketDataOptions> options, TimeProvider time, ILogger<MarketDataSyncService> logger)
    {
        _sync = sync;
        _time = time;
        _enabled = options.Value.Downloader.Enabled;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("The Downloader is switched off: TradeViewer works with the files and the candles it has.");
            return;
        }

        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                var next = await _sync.RunOnceAsync(stoppingToken);
                delay = next - _time.GetUtcNow();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The sync with the Downloader failed; next try in {Delay}.", AfterAnError);
                delay = AfterAnError;
            }

            try
            {
                await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1), _time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
