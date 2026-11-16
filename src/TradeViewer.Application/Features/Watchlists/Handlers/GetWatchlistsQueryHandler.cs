using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Watchlists.Queries;
using TradeViewer.Contracts.Watchlists;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.Watchlists.Handlers;

public sealed class GetWatchlistsQueryHandler : IQueryHandler<GetWatchlistsQuery, IReadOnlyList<WatchlistResponse>>
{
    /// <summary>The daily candle of today may be delivered already; the one before it is needed too.</summary>
    private const int DailyCandles = 2;

    private readonly IAssetRepository _assets;
    private readonly ICandleStore _store;
    private readonly IImportQueue _imports;

    public GetWatchlistsQueryHandler(IAssetRepository assets, ICandleStore store, IImportQueue imports)
    {
        _assets = assets;
        _store = store;
        _imports = imports;
    }

    public async Task<Result<IReadOnlyList<WatchlistResponse>>> HandleAsync(GetWatchlistsQuery query, CancellationToken cancellationToken)
    {
        var assets = await _assets.GetAllAsync(cancellationToken);

        // The newest candles of all rows in two reads: the last close, and the daily closes for the change.
        var withCandles = assets.Where(asset => asset.History.Timeframes.Count > 0).ToList();
        var smallest = _store.GetNewestCandles(withCandles.Select(asset => new SeriesKey(asset.Source, asset.Symbol, asset.History.Timeframes[0])).ToList(), 1);
        var daily = _store.GetNewestCandles(
            withCandles.Where(asset => asset.History.Has(Timeframe.D1)).Select(asset => new SeriesKey(asset.Source, asset.Symbol, Timeframe.D1)).ToList(),
            DailyCandles);

        var lists = new List<WatchlistResponse>();
        foreach (var assetClass in WatchlistOrder.Lists)
        {
            var rows = assets
                .Where(asset => asset.Info.AssetClass == assetClass)
                .OrderBy(asset => asset.Position)
                .Select(asset => ToRow(asset, smallest, daily))
                .ToList();

            lists.Add(new WatchlistResponse(AssetCodes.Code(assetClass), WatchlistOrder.Title(assetClass), rows));
        }

        return Result.Success<IReadOnlyList<WatchlistResponse>>(lists);
    }

    private WatchlistRowResponse ToRow(
        Asset asset,
        IReadOnlyDictionary<SeriesKey, IReadOnlyList<Candle>> smallest,
        IReadOnlyDictionary<SeriesKey, IReadOnlyList<Candle>> daily)
    {
        RowFigures? figures = null;
        if (asset.History.Timeframes.Count > 0)
        {
            var timeframe = asset.History.Timeframes[0];
            smallest.TryGetValue(new SeriesKey(asset.Source, asset.Symbol, timeframe), out var last);
            daily.TryGetValue(new SeriesKey(asset.Source, asset.Symbol, Timeframe.D1), out var days);

            Candle? lastCandle = last == null || last.Count == 0 ? null : last[last.Count - 1];
            figures = RowFigures.Of(timeframe, lastCandle, days ?? Array.Empty<Candle>());
        }

        var status = _store.GetSyncState(asset.Source, asset.Symbol)?.LastStatus;
        var chartable = _store.GetRanges(asset.Source, asset.Symbol).Select(range => Timeframes.Code(range.Timeframe)).ToList();

        return new WatchlistRowResponse(
            asset.Source,
            asset.Symbol,
            asset.Info.DisplayName,
            asset.Info.TickSize,
            figures?.LastClose,
            figures == null ? null : DateTimeOffset.FromUnixTimeSeconds(figures.LastCloseTime),
            figures?.ChangePercent,
            _imports.GetProgress(asset.Source, asset.Symbol).Pending || DownloaderStatusRules.IsDownloading(status),
            Warning(_store.GetImportErrors(asset.Source, asset.Symbol).Count, DownloaderStatusRules.MissingUpstreamGaps(status)),
            chartable,
            Array.Empty<string>()); // filled by the epic EWTS Instances (EW3)
    }

    /// <summary>Why the asset needs a look, or null (AS6, MD4, MD6).</summary>
    public static string? Warning(int filesNotImported, int missingGaps)
    {
        var reasons = new List<string>();
        if (filesNotImported > 0)
        {
            reasons.Add(filesNotImported == 1 ? "1 file was not imported" : filesNotImported + " files were not imported");
        }

        if (missingGaps > 0)
        {
            reasons.Add(missingGaps == 1 ? "1 gap the source does not have candles for" : missingGaps + " gaps the source does not have candles for");
        }

        return reasons.Count == 0 ? null : string.Join("; ", reasons);
    }
}
