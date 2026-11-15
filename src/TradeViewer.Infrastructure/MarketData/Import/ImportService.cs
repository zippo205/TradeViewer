using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Infrastructure.MarketData.Import;

/// <summary>
/// The one worker that writes the candle store. It takes the tasks in the order they were asked for. At the
/// start it rescans every asset in the background, so the application is usable at once (NF10).
/// </summary>
public sealed class ImportService : BackgroundService, IImportQueue
{
    private readonly Channel<Work> _channel = Channel.CreateUnbounded<Work>(new UnboundedChannelOptions { SingleReader = true });
    private readonly object _sync = new object();
    private readonly Dictionary<(string Source, string Symbol), int> _pending = new Dictionary<(string, string), int>();
    private readonly HashSet<(string Source, string Symbol)> _queuedRescans = new HashSet<(string, string)>();
    private readonly Dictionary<(string Source, string Symbol), Dictionary<Timeframe, TimeframeProgress>> _progress =
        new Dictionary<(string, string), Dictionary<Timeframe, TimeframeProgress>>();

    private readonly IAssetRepository _assets;
    private readonly AssetFiles _files;
    private readonly AssetImporter _importer;
    private readonly ICandleStore _store;
    private readonly ILogger<ImportService> _logger;

    private Work? _current;
    private CancellationTokenSource? _currentCancel;

    public ImportService(IAssetRepository assets, AssetFiles files, AssetImporter importer, ICandleStore store, ILogger<ImportService> logger)
    {
        _assets = assets;
        _files = files;
        _importer = importer;
        _store = store;
        _logger = logger;
    }

    /// <summary>True when nothing is queued or running.</summary>
    public bool IsIdle
    {
        get
        {
            lock (_sync)
            {
                return _pending.Count == 0;
            }
        }
    }

    public void Rescan(string source, string symbol)
    {
        lock (_sync)
        {
            // One waiting rescan is enough; it will see everything a second one would.
            if (!_queuedRescans.Add((source, symbol)))
            {
                return;
            }
        }

        Enqueue(new Work(WorkKind.Rescan, source, symbol, null));
    }

    public void Rebuild(string source, string symbol)
    {
        Enqueue(new Work(WorkKind.Rebuild, source, symbol, null));
    }

    public void ImportFiles(string source, string symbol, IReadOnlyList<string> files)
    {
        Enqueue(new Work(WorkKind.Files, source, symbol, files));
    }

    public void Delete(string source, string symbol)
    {
        Enqueue(new Work(WorkKind.Delete, source, symbol, null));
    }

    public ImportProgress GetProgress(string source, string symbol)
    {
        lock (_sync)
        {
            var key = (source, symbol);
            var timeframes = _progress.TryGetValue(key, out var progress)
                ? progress.Values.OrderByDescending(item => Timeframes.Seconds(item.Timeframe)).ToList()
                : new List<TimeframeProgress>();

            return new ImportProgress(_pending.ContainsKey(key), timeframes);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // the start of the application does not wait for the first import

        try
        {
            foreach (var asset in await _assets.GetAllAsync(stoppingToken))
            {
                Rescan(asset.Source, asset.Symbol);
            }

            await foreach (var work in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                await RunAsync(work, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // the application stops
        }
    }

    private void Enqueue(Work work)
    {
        lock (_sync)
        {
            var key = (work.Source, work.Symbol);
            _pending[key] = _pending.TryGetValue(key, out var count) ? count + 1 : 1;

            // A running import of the same asset is pointless once its candles are to be deleted.
            if ((work.Kind == WorkKind.Delete || work.Kind == WorkKind.Rebuild)
                && _current != null && _current.Source == work.Source && _current.Symbol == work.Symbol)
            {
                _currentCancel?.Cancel();
            }
        }

        _channel.Writer.TryWrite(work);
    }

    private async Task RunAsync(Work work, CancellationToken stoppingToken)
    {
        var key = (work.Source, work.Symbol);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        lock (_sync)
        {
            _current = work;
            _currentCancel = cancel;
            if (work.Kind == WorkKind.Rescan)
            {
                _queuedRescans.Remove(key);
            }
        }

        try
        {
            await DoAsync(work, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            // stopped for a delete or a rebuild of the asset, or the application stops
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The import of {Source}/{Symbol} failed ({Kind}).", work.Source, work.Symbol, work.Kind);
        }
        finally
        {
            lock (_sync)
            {
                _current = null;
                _currentCancel = null;

                if (_pending[key] == 1)
                {
                    _pending.Remove(key);
                }
                else
                {
                    _pending[key]--;
                }
            }
        }
    }

    private async Task DoAsync(Work work, CancellationToken cancellationToken)
    {
        var key = (work.Source, work.Symbol);

        if (work.Kind == WorkKind.Delete || work.Kind == WorkKind.Rebuild)
        {
            _store.DeleteAsset(work.Source, work.Symbol);
            lock (_sync)
            {
                _progress.Remove(key);
            }

            if (work.Kind == WorkKind.Delete)
            {
                return;
            }
        }

        var asset = await _assets.GetAsync(work.Source, work.Symbol, cancellationToken);
        if (asset == null)
        {
            return; // removed meanwhile
        }

        if (work.Kind == WorkKind.Files)
        {
            _importer.ImportFiles(asset, work.Files!);
            return;
        }

        if (!_files.HasFolder(asset.Source, asset.Symbol))
        {
            _logger.LogWarning("{Source}/{Symbol} has no folder in the Downloader's output root: no data.", asset.Source, asset.Symbol);
        }

        _importer.Rescan(asset, progress => SetProgress(key, progress), cancellationToken);
    }

    private void SetProgress((string Source, string Symbol) key, TimeframeProgress progress)
    {
        lock (_sync)
        {
            if (!_progress.TryGetValue(key, out var timeframes))
            {
                timeframes = new Dictionary<Timeframe, TimeframeProgress>();
                _progress[key] = timeframes;
            }

            timeframes[progress.Timeframe] = progress;
        }
    }

    private enum WorkKind
    {
        Rescan,
        Rebuild,
        Files,
        Delete
    }

    private sealed record Work(WorkKind Kind, string Source, string Symbol, IReadOnlyList<string>? Files);
}
