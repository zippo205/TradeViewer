using System.Globalization;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Assets.Queries;
using TradeViewer.Application.Features.Assets.Services;
using TradeViewer.Contracts.Assets;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.Assets.Handlers;

/// <summary>
/// A symbol picked, looked up at the Downloader by its ticker before it is added (AS2): tick size, session and the
/// earliest date per timeframe. The catalog promises no candles; this lookup is the check. Without the Downloader
/// the fixed list answers, without dates.
/// </summary>
public sealed class GetSymbolDetailsQueryHandler : IQueryHandler<GetSymbolDetailsQuery, SymbolDetailsResponse>
{
    private readonly ISymbolCatalog _catalog;
    private readonly IAssetRepository _assets;
    private readonly IDownloaderClient _downloader;

    public GetSymbolDetailsQueryHandler(ISymbolCatalog catalog, IAssetRepository assets, IDownloaderClient downloader)
    {
        _catalog = catalog;
        _assets = assets;
        _downloader = downloader;
    }

    public async Task<Result<SymbolDetailsResponse>> HandleAsync(GetSymbolDetailsQuery query, CancellationToken cancellationToken)
    {
        if (!SymbolSources.IsChartable(query.Source))
        {
            return Error.NotFound("source.notFound", "Symbols of the source " + query.Source + " cannot be added: TradeViewer cannot chart them yet.");
        }

        SymbolInfo info;
        IReadOnlyDictionary<Timeframe, DateOnly> earliest;
        if (_downloader.Enabled)
        {
            var lookup = await _downloader.LookupAsync(query.Source, query.Ticker, cancellationToken);
            if (lookup.IsFailure)
            {
                return lookup.Error;
            }

            info = lookup.Value.Info;
            earliest = lookup.Value.Earliest;
        }
        else
        {
            var listed = _catalog.Find(query.Source, query.Ticker);
            if (listed == null)
            {
                return AssetErrors.UnknownSymbol(query.Source, query.Ticker);
            }

            info = listed;
            earliest = new Dictionary<Timeframe, DateOnly>();
        }

        var added = await _assets.GetAsync(info.Source, info.Symbol, cancellationToken) != null;
        return Result.Success(new SymbolDetailsResponse(
            info.Source,
            info.Symbol,
            info.Ticker,
            info.DisplayName,
            AssetCodes.Code(info.AssetClass),
            info.TickSize,
            AssetCodes.Code(info.Session.Type),
            info.Session.TimeZone,
            info.Session.Open,
            info.Session.Close,
            info.SourceTimeframes.Select(Timeframes.Code).ToList(),
            earliest.OrderBy(pair => Timeframes.Seconds(pair.Key)).ToDictionary(
                pair => Timeframes.Code(pair.Key),
                pair => pair.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            added));
    }
}
