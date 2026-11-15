using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Assets.Queries;
using TradeViewer.Contracts.Assets;
using TradeViewer.Domain.Assets;

namespace TradeViewer.Application.Features.Assets.Handlers;

public sealed class GetSymbolsQueryHandler : IQueryHandler<GetSymbolsQuery, IReadOnlyList<SymbolResponse>>
{
    private readonly ISymbolCatalog _catalog;
    private readonly IAssetRepository _assets;

    public GetSymbolsQueryHandler(ISymbolCatalog catalog, IAssetRepository assets)
    {
        _catalog = catalog;
        _assets = assets;
    }

    public async Task<Result<IReadOnlyList<SymbolResponse>>> HandleAsync(GetSymbolsQuery query, CancellationToken cancellationToken)
    {
        if (!_catalog.Sources.Contains(query.Source))
        {
            return Error.NotFound("source.notFound", "There is no symbol list for the source " + query.Source + ".");
        }

        var added = (await _assets.GetAllAsync(cancellationToken))
            .Where(asset => asset.Source == query.Source)
            .Select(asset => asset.Symbol)
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlyList<SymbolResponse> symbols = _catalog.Search(query.Source, query.Text)
            .Select(symbol => new SymbolResponse(
                symbol.Source,
                symbol.Symbol,
                symbol.DisplayName,
                AssetCodes.Code(symbol.AssetClass),
                added.Contains(symbol.Symbol)))
            .ToList();

        return Result.Success(symbols);
    }
}
