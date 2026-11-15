using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.MarketData.Import;

namespace TradeViewer.IntegrationTests.MarketData;

/// <summary>The background worker: startup rescan (NF10), rebuild (MD5), delete (AS5), progress (AS4).</summary>
public class ImportServiceTests : IDisposable
{
    private static readonly AssetHistory Complete = MarketDataFixture.Asset("X").History;

    private readonly MarketDataFixture _data = new MarketDataFixture();
    private ImportService? _service;

    public void Dispose()
    {
        _service?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _service?.Dispose();
        _data.Dispose();
    }

    [Fact]
    public async Task At_the_start_every_asset_is_rescanned_in_the_background()
    {
        await AddAssetAsync("BTCUSDT");
        await AddAssetAsync("ETHUSDT");
        var btc = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        var eth = _data.WriteFile("ETHUSDT", "ETHUSDT-1d-2026-11-04.csv", 1);
        _data.WriteFile("SOLUSDT", "SOLUSDT-1d-2026-11-04.csv", 1); // not an asset of TradeViewer

        await StartAsync();

        Assert.Equal(btc, _data.Candles("BTCUSDT", Timeframe.M15));
        Assert.Equal(eth, _data.Candles("ETHUSDT", Timeframe.D1));
        Assert.Empty(_data.Candles("SOLUSDT", Timeframe.D1));
    }

    [Fact]
    public async Task The_progress_tells_what_is_imported_per_timeframe_and_whether_work_is_pending()
    {
        await AddAssetAsync("BTCUSDT");
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-03.csv", 96);
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        _data.WriteFile("BTCUSDT", "BTCUSDT-1d-2026-11-04.csv", 1);
        _service = _data.NewImportService();

        AssertNoProgress("BTCUSDT");

        _service.Rescan("binance", "BTCUSDT");
        Assert.True(_service.GetProgress("binance", "BTCUSDT").Pending); // queued, the worker is not even started

        await StartAsync();
        var progress = _service.GetProgress("binance", "BTCUSDT");

        Assert.False(progress.Pending);
        Assert.Equal(
            new[]
            {
                new TimeframeProgress(Timeframe.W1, 0, 0),
                new TimeframeProgress(Timeframe.D1, 1, 1),
                new TimeframeProgress(Timeframe.H4, 0, 0),
                new TimeframeProgress(Timeframe.H1, 0, 0),
                new TimeframeProgress(Timeframe.M15, 2, 2)
            },
            progress.Timeframes);
        Assert.False(_service.GetProgress("binance", "ETHUSDT").Pending);
    }

    [Fact]
    public async Task A_rebuild_deletes_the_candles_of_the_asset_and_imports_them_again()
    {
        await AddAssetAsync("BTCUSDT");
        await AddAssetAsync("ETHUSDT");
        var btc = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        var eth = _data.WriteFile("ETHUSDT", "ETHUSDT-15m-2026-11-04.csv", 96);
        await StartAsync();

        // Damage in the store that no rescan would find: the files did not change.
        var series = new SeriesKey("binance", "BTCUSDT", Timeframe.M15);
        CandleFileRules.TryParseName("BTCUSDT-15m-2026-11-04.csv", out var file);
        var entry = _data.Store.GetImportLog(series)[file!.Name];
        _data.Store.Import(series, new[] { new FileImport(file, entry.SizeBytes, entry.Modified, btc.Take(10).ToList(), null) });
        _service!.Rescan("binance", "BTCUSDT");
        await WaitUntilIdleAsync();
        Assert.Equal(10, _data.Candles("BTCUSDT", Timeframe.M15).Count);

        _service.Rebuild("binance", "BTCUSDT");
        await WaitUntilIdleAsync();

        Assert.Equal(btc, _data.Candles("BTCUSDT", Timeframe.M15));
        Assert.Equal(eth, _data.Candles("ETHUSDT", Timeframe.M15));
    }

    [Fact]
    public async Task Deleting_removes_the_asset_from_the_store_even_when_a_rescan_was_asked_before()
    {
        await AddAssetAsync("BTCUSDT");
        await AddAssetAsync("ETHUSDT");
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        var eth = _data.WriteFile("ETHUSDT", "ETHUSDT-15m-2026-11-04.csv", 96);
        _service = _data.NewImportService();

        // The order of a removal: the asset leaves the workspace, then its candles are deleted.
        _service.Rescan("binance", "BTCUSDT");
        await _data.Workspace.Assets.RemoveAsync("binance", "BTCUSDT", false, CancellationToken.None);
        _service.Delete("binance", "BTCUSDT");
        await StartAsync();

        Assert.Empty(_data.Candles("BTCUSDT", Timeframe.M15));
        Assert.Empty(_data.Store.GetImportLog(new SeriesKey("binance", "BTCUSDT", Timeframe.M15)));
        Assert.Equal(eth, _data.Candles("ETHUSDT", Timeframe.M15));
        AssertNoProgress("BTCUSDT");
    }

    [Fact]
    public async Task Changed_files_named_by_the_Downloader_are_imported_by_the_worker()
    {
        await AddAssetAsync("BTCUSDT");
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 30);
        await StartAsync();

        var grown = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 31);
        _service!.ImportFiles("binance", "BTCUSDT", new[] { "15m/BTCUSDT-15m-2026-11-04.csv" });
        await WaitUntilIdleAsync();

        Assert.Equal(grown, _data.Candles("BTCUSDT", Timeframe.M15));
    }

    [Fact]
    public async Task A_failing_task_does_not_stop_the_worker()
    {
        await AddAssetAsync("BTCUSDT");
        var candles = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        await StartAsync();

        // A folder where a file is expected makes the reader throw an error nobody planned for.
        Directory.CreateDirectory(_data.FilePath("BTCUSDT", "BTCUSDT-15m-2026-11-05.csv"));
        _service!.Rescan("binance", "BTCUSDT");
        await WaitUntilIdleAsync();
        Directory.Delete(_data.FilePath("BTCUSDT", "BTCUSDT-15m-2026-11-05.csv"));

        var next = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-05.csv", 5);
        _service.Rescan("binance", "BTCUSDT");
        await WaitUntilIdleAsync();

        Assert.Equal(candles.Concat(next), _data.Candles("BTCUSDT", Timeframe.M15));
    }

    private void AssertNoProgress(string symbol)
    {
        var progress = _service!.GetProgress("binance", symbol);

        Assert.False(progress.Pending);
        Assert.Empty(progress.Timeframes);
    }

    private Task AddAssetAsync(string symbol)
    {
        return _data.Workspace.Assets.AddAsync(MarketDataFixture.Symbol(symbol), Complete, CancellationToken.None);
    }

    private async Task StartAsync()
    {
        _service ??= _data.NewImportService();
        await _service.StartAsync(CancellationToken.None);
        await WaitUntilIdleAsync();
    }

    private async Task WaitUntilIdleAsync()
    {
        // Idle twice in a row: the startup rescan is queued by the worker itself, a moment after the start.
        var idle = 0;
        for (var attempt = 0; attempt < 1000 && idle < 3; attempt++)
        {
            await Task.Delay(10);
            idle = _service!.IsIdle ? idle + 1 : 0;
        }

        Assert.True(_service!.IsIdle, "The import worker did not finish.");
    }
}
