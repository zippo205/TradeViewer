using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.MarketData.Queries;
using TradeViewer.Contracts.MarketData;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.MarketData.Handlers;

public sealed class GetGapsQueryHandler : IQueryHandler<GetGapsQuery, IReadOnlyList<GapResponse>>
{
    private readonly IAssetRepository _assets;
    private readonly ICandleStore _store;

    public GetGapsQueryHandler(IAssetRepository assets, ICandleStore store)
    {
        _assets = assets;
        _store = store;
    }

    public async Task<Result<IReadOnlyList<GapResponse>>> HandleAsync(GetGapsQuery query, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(query.Source, query.Symbol, cancellationToken);
        if (asset == null)
        {
            return Error.NotFound("asset.notFound", "There is no asset " + query.Source + "/" + query.Symbol + ".");
        }

        if (!Timeframes.TryParse(query.Timeframe, out var timeframe) || !asset.History.Has(timeframe))
        {
            return Error.InvalidData("candles.timeframe", query.Source + "/" + query.Symbol + " has no timeframe " + query.Timeframe + ".");
        }

        // The last status the Downloader reported; without the Downloader nothing is known about gaps.
        var status = _store.GetSyncState(asset.Source, asset.Symbol)?.LastStatus;
        var ofTimeframe = status?.Timeframes.FirstOrDefault(item => item.Timeframe == timeframe);
        IReadOnlyList<GapResponse> gaps = DownloaderStatusRules.ConfirmedGaps(ofTimeframe, asset.History.StartSecondsOf(timeframe))
            .Select(gap => new GapResponse(gap.From.ToUnixTimeSeconds(), gap.To.ToUnixTimeSeconds(), gap.Reason))
            .Where(gap => gap.To > gap.From)
            .ToList();

        return Result.Success(gaps);
    }
}
