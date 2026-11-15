using Microsoft.Extensions.Logging;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Infrastructure.MarketData.Import;

/// <summary>
/// Brings the candle store in line with the files of an asset. Large timeframes first and, within a timeframe,
/// the newest files first: a chart can show the latest candles after seconds while the long history of the
/// small timeframes follows (AS4).
/// </summary>
public sealed class AssetImporter
{
    /// <summary>Files per transaction: large enough to be fast, small enough for regular progress.</summary>
    public const int BatchSize = 200;

    private readonly AssetFiles _files;
    private readonly ICandleFileReader _reader;
    private readonly ICandleStore _store;
    private readonly ILogger<AssetImporter> _logger;

    public AssetImporter(AssetFiles files, ICandleFileReader reader, ICandleStore store, ILogger<AssetImporter> logger)
    {
        _files = files;
        _reader = reader;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Compares every wanted file with the import log (size and modified time), imports what is new or changed
    /// and forgets what is no longer there or no longer wanted.
    /// </summary>
    /// <param name="asset">The asset.</param>
    /// <param name="report">Called with the progress of a timeframe, at its start and after every batch.</param>
    /// <param name="cancellationToken">Stops between two batches; what is imported stays.</param>
    /// <returns>The timeframes whose candles changed.</returns>
    public IReadOnlyList<Timeframe> Rescan(Asset asset, Action<TimeframeProgress> report, CancellationToken cancellationToken)
    {
        var changedTimeframes = new List<Timeframe>();

        foreach (var timeframe in asset.History.Timeframes.Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var series = new SeriesKey(asset.Source, asset.Symbol, timeframe);
            var wanted = _files.Wanted(asset, timeframe);
            var log = _store.GetImportLog(series);

            var wantedNames = new HashSet<string>(wanted.Select(file => file.File.Name), StringComparer.Ordinal);
            var gone = new List<CandleFile>();
            foreach (var name in log.Keys)
            {
                if (!wantedNames.Contains(name) && CandleFileRules.TryParseName(name, out var file))
                {
                    gone.Add(file!);
                }
            }

            var changed = wanted.Where(file => IsChanged(file, log)).ToList();
            var done = wanted.Count - changed.Count;
            report(new TimeframeProgress(timeframe, done, wanted.Count));

            if (gone.Count > 0)
            {
                _store.RemoveFiles(series, gone);
            }

            for (var index = 0; index < changed.Count; index += BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = changed.Skip(index).Take(BatchSize).ToList();
                done += Import(series, batch);
                report(new TimeframeProgress(timeframe, done, wanted.Count));
            }

            if (gone.Count > 0 || changed.Count > 0)
            {
                changedTimeframes.Add(timeframe);
            }
        }

        return changedTimeframes;
    }

    /// <summary>Imports the files the Downloader named as changed; names that are not wanted files of the asset are ignored.</summary>
    /// <returns>The timeframes whose candles changed.</returns>
    public IReadOnlyList<Timeframe> ImportFiles(Asset asset, IReadOnlyList<string> relativeNames)
    {
        var byTimeframe = new Dictionary<Timeframe, List<SourceFile>>();
        foreach (var name in relativeNames.Distinct(StringComparer.Ordinal))
        {
            var file = _files.Find(asset, name);
            if (file == null)
            {
                continue;
            }

            if (!byTimeframe.TryGetValue(file.File.Timeframe, out var list))
            {
                list = new List<SourceFile>();
                byTimeframe[file.File.Timeframe] = list;
            }

            list.Add(file);
        }

        foreach (var pair in byTimeframe)
        {
            Import(new SeriesKey(asset.Source, asset.Symbol, pair.Key), pair.Value);
        }

        return byTimeframe.Keys.OrderBy(Timeframes.Seconds).ToList();
    }

    private static bool IsChanged(SourceFile file, IReadOnlyDictionary<string, ImportLogEntry> log)
    {
        return !log.TryGetValue(file.File.Name, out var entry) || entry.SizeBytes != file.SizeBytes || entry.Modified != file.Modified;
    }

    /// <returns>The number of files that are in the store afterwards (files that vanished meanwhile do not count).</returns>
    private int Import(SeriesKey series, IReadOnlyList<SourceFile> files)
    {
        var imports = new List<FileImport>(files.Count);
        foreach (var file in files)
        {
            var result = _reader.Read(file.Path, file.File);
            if (result.IsSuccess)
            {
                imports.Add(new FileImport(file.File, file.SizeBytes, file.Modified, result.Value, null));
            }
            else if (result.Error.Type == ErrorType.InvalidData)
            {
                // Not imported; the chart works without these candles and the asset shows the warning (MD6).
                _logger.LogWarning("Candle file refused: {Source}/{Symbol} {Message}", series.Source, series.Symbol, result.Error.Message);
                imports.Add(new FileImport(file.File, file.SizeBytes, file.Modified, Array.Empty<Candle>(), result.Error.Message));
            }

            // A file that is gone (replaced just now) is left to the next change or rescan.
        }

        _store.Import(series, imports);
        return imports.Count;
    }
}
