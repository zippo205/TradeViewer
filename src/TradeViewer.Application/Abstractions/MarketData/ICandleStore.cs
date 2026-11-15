using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>
/// The candle store (marketdata.db): a copy of the Downloader's files that charts read fast. It holds nothing
/// the user created and can be deleted and filled again at any time (MD5).
/// </summary>
public interface ICandleStore
{
    /// <summary>Imports files of one series in one transaction: each file replaces its period and gets its entry in the import log.</summary>
    void Import(SeriesKey series, IReadOnlyList<FileImport> files);

    /// <summary>The import log of a series by file name.</summary>
    IReadOnlyDictionary<string, ImportLogEntry> GetImportLog(SeriesKey series);

    /// <summary>Forgets files that no longer exist: their candles and their entries in the import log.</summary>
    void RemoveFiles(SeriesKey series, IReadOnlyList<CandleFile> files);

    /// <summary>The newest candles that open before the time (all when null), at most the limit, oldest first.</summary>
    IReadOnlyList<Candle> GetCandles(SeriesKey series, long? before, int limit);

    /// <summary>The candles that open at or after the time, oldest first.</summary>
    IReadOnlyList<Candle> GetCandlesFrom(SeriesKey series, long from);

    /// <summary>The timeframes of an asset that hold candles.</summary>
    IReadOnlyList<SeriesRange> GetRanges(string source, string symbol);

    /// <summary>The files of an asset that broke the format rules, with the message of the reader.</summary>
    IReadOnlyList<ImportLogEntry> GetImportErrors(string source, string symbol);

    /// <summary>Deletes the candles before the time and the log entries of the files that end at or before it.</summary>
    void DeleteBefore(SeriesKey series, long time);

    /// <summary>Deletes everything of an asset: candles, import log and sync state.</summary>
    void DeleteAsset(string source, string symbol);

    SyncState? GetSyncState(string source, string symbol);

    void SetSyncState(string source, string symbol, SyncState state);
}
