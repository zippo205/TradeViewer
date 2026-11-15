using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.Storage;

namespace TradeViewer.Infrastructure.MarketData.Import;

/// <summary>
/// Finds the candle files of an asset in the Downloader's output root (read only):
/// {root}/{source}/{SYMBOL}/{timeframe}/{SYMBOL}-{timeframe}-{period}.csv. Only *.csv is read; a missing
/// folder means "no data".
/// </summary>
public sealed class AssetFiles : IAssetFolders
{
    private readonly StorageLocations _locations;

    public AssetFiles(StorageLocations locations)
    {
        _locations = locations;
    }

    /// <summary>True when the output root has a folder for the symbol.</summary>
    public bool HasFolder(string source, string symbol)
    {
        return IsFolderName(source) && IsFolderName(symbol) && Directory.Exists(SymbolFolder(source, symbol));
    }

    /// <summary>The files of one timeframe that the history of the asset wants, newest first.</summary>
    public IReadOnlyList<SourceFile> Wanted(Asset asset, Timeframe timeframe)
    {
        var folder = Path.Combine(SymbolFolder(asset.Source, asset.Symbol), Timeframes.Code(timeframe));
        if (!asset.History.Has(timeframe) || !Directory.Exists(folder))
        {
            return Array.Empty<SourceFile>();
        }

        var files = new List<SourceFile>();
        foreach (var info in new DirectoryInfo(folder).EnumerateFiles("*.csv"))
        {
            if (CandleFileRules.TryParseName(info.Name, out var file)
                && file!.Symbol == asset.Symbol
                && file.Timeframe == timeframe
                && asset.History.Wants(file))
            {
                files.Add(new SourceFile(file, info.FullName, info.Length, new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds()));
            }
        }

        return files.OrderByDescending(file => file.File.PeriodStart).ToList();
    }

    /// <summary>
    /// The file behind a name the Downloader reported, relative to the symbol folder (15m/BTCUSDT-15m-2026-11-04.csv).
    /// Null when the name is not a wanted candle file of the asset or the file does not exist.
    /// </summary>
    public SourceFile? Find(Asset asset, string relativeName)
    {
        var parts = relativeName.Split('/');
        if (parts.Length != 2
            || !Timeframes.TryParse(parts[0], out var timeframe)
            || parts[1].Contains('\\')
            || !CandleFileRules.TryParseName(parts[1], out var file)
            || file!.Symbol != asset.Symbol
            || file.Timeframe != timeframe
            || !asset.History.Has(timeframe)
            || !asset.History.Wants(file))
        {
            return null;
        }

        var info = new FileInfo(Path.Combine(SymbolFolder(asset.Source, asset.Symbol), parts[0], parts[1]));
        return info.Exists
            ? new SourceFile(file, info.FullName, info.Length, new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds())
            : null;
    }

    private string SymbolFolder(string source, string symbol)
    {
        return Path.Combine(_locations.MarketDataRoot, source, symbol);
    }

    /// <summary>Source and symbol become folder names, so they must not lead anywhere else.</summary>
    private static bool IsFolderName(string value)
    {
        return value.Length > 0 && value != "." && value != ".." && value.IndexOfAny(new[] { '/', '\\' }) < 0;
    }
}
