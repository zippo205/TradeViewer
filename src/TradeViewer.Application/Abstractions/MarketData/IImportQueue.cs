namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>
/// The work on the candle store. Everything is done by one background worker in the order it was asked for,
/// so a request returns at once and two tasks never write the same asset at the same time.
/// </summary>
public interface IImportQueue
{
    /// <summary>Imports the files of the asset that are new or changed and forgets those that are no longer wanted.</summary>
    void Rescan(string source, string symbol);

    /// <summary>Deletes the candles of the asset and imports everything again (MD5).</summary>
    void Rebuild(string source, string symbol);

    /// <summary>Imports the files the Downloader named as changed, relative to the symbol folder (e.g. 15m/BTCUSDT-15m-2026-11-04.csv).</summary>
    void ImportFiles(string source, string symbol, IReadOnlyList<string> files);

    /// <summary>Deletes everything the candle store holds of a removed asset.</summary>
    void Delete(string source, string symbol);

    ImportProgress GetProgress(string source, string symbol);
}
