using System.Globalization;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Contracts.Assets;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.Assets.Services;

/// <summary>
/// Puts the state of an asset together: what the candle store holds, how far the import is and, when the
/// Downloader was asked, what it still downloads (AS4).
/// </summary>
public sealed class AssetStatusReader
{
    private readonly ICandleStore _store;
    private readonly IImportQueue _imports;

    public AssetStatusReader(ICandleStore store, IImportQueue imports)
    {
        _store = store;
        _imports = imports;
    }

    public AssetResponse Read(Asset asset)
    {
        return Build(
            asset,
            _store.GetRanges(asset.Source, asset.Symbol),
            _imports.GetProgress(asset.Source, asset.Symbol),
            _store.GetSyncState(asset.Source, asset.Symbol)?.LastStatus,
            _store.GetImportErrors(asset.Source, asset.Symbol));
    }

    /// <summary>The rules of the state, without any access to a store.</summary>
    public static AssetResponse Build(
        Asset asset,
        IReadOnlyList<SeriesRange> ranges,
        ImportProgress import,
        SymbolStatus? downloader,
        IReadOnlyList<ImportLogEntry> errors)
    {
        var timeframes = new List<AssetTimeframeResponse>();
        foreach (var timeframe in asset.History.Timeframes)
        {
            var range = ranges.FirstOrDefault(item => item.Timeframe == timeframe);
            var progress = import.Timeframes.FirstOrDefault(item => item.Timeframe == timeframe);
            var status = downloader?.Timeframes.FirstOrDefault(item => item.Timeframe == timeframe);

            // While work for the asset is pending, a timeframe the import has not looked at yet counts as loading too.
            var importing = import.Pending && (progress == null || progress.FilesImported < progress.FilesWanted);

            timeframes.Add(new AssetTimeframeResponse(
                Timeframes.Code(timeframe),
                asset.History.StartOf(timeframe)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                range == null ? null : DateTimeOffset.FromUnixTimeSeconds(range.FirstTime),
                range == null ? null : DateTimeOffset.FromUnixTimeSeconds(range.LastTime),
                progress?.FilesImported ?? 0,
                progress?.FilesWanted ?? 0,
                importing || DownloaderStatusRules.IsDownloading(status),
                range != null));
        }

        return new AssetResponse(
            asset.Source,
            asset.Symbol,
            asset.Info.DisplayName,
            AssetCodes.Code(asset.Info.AssetClass),
            asset.Info.TickSize,
            asset.Position,
            import.Pending || timeframes.Any(timeframe => timeframe.Loading),
            !asset.SubscriptionSent,
            timeframes,
            errors.Select(error => error.Error ?? error.File).ToList());
    }
}
