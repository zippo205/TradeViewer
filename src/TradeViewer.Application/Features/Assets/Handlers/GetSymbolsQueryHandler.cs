using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Assets.Queries;
using TradeViewer.Contracts.Assets;
using TradeViewer.Domain.Assets;

namespace TradeViewer.Application.Features.Assets.Handlers;

/// <summary>
/// The symbols of a source that can be added (AS2): with the Downloader its symbol catalog, at most
/// <see cref="Limit"/> per answer; without it the fixed list of the source.
/// </summary>
public sealed class GetSymbolsQueryHandler : IQueryHandler<GetSymbolsQuery, SymbolSearchResponse>
{
    /// <summary>The most symbols one answer shows; more matches ask the user to type more.</summary>
    public const int Limit = 50;

    /// <summary>The longest search text the Downloader takes.</summary>
    public const int MaxTextLength = 40;

    private readonly ISymbolCatalog _catalog;
    private readonly IAssetRepository _assets;
    private readonly IDownloaderClient _downloader;

    public GetSymbolsQueryHandler(ISymbolCatalog catalog, IAssetRepository assets, IDownloaderClient downloader)
    {
        _catalog = catalog;
        _assets = assets;
        _downloader = downloader;
    }

    public async Task<Result<SymbolSearchResponse>> HandleAsync(GetSymbolsQuery query, CancellationToken cancellationToken)
    {
        if (!SymbolSources.IsChartable(query.Source))
        {
            return Error.NotFound("source.notFound", "Symbols of the source " + query.Source + " cannot be added: TradeViewer cannot chart them yet.");
        }

        var text = (query.Text ?? string.Empty).Trim();
        if (text.Length > MaxTextLength)
        {
            return Error.Validation(
                "symbols.text",
                "The search text is too long.",
                new Dictionary<string, string[]> { ["q"] = new[] { "At most " + MaxTextLength + " characters." } });
        }

        var added = (await _assets.GetAllAsync(cancellationToken))
            .Where(asset => asset.Source == query.Source)
            .Select(asset => asset.Symbol)
            .ToHashSet(StringComparer.Ordinal);

        if (!_downloader.Enabled)
        {
            // Without the Downloader the fixed list of the source is the catalog (AS2).
            var listed = _catalog.Search(query.Source, text)
                .Select(symbol => new SymbolResponse(symbol.Source, symbol.Symbol, symbol.DisplayName, AssetCodes.Code(symbol.AssetClass), added.Contains(symbol.Symbol)))
                .ToList();
            return Result.Success(new SymbolSearchResponse(listed, listed.Count, false, false));
        }

        var page = await _downloader.SearchAsync(query.Source, text, Limit, cancellationToken);
        if (page.IsFailure)
        {
            return page.Error;
        }

        var symbols = page.Value.Symbols
            .Select(entry => new SymbolResponse(
                query.Source,
                entry.Symbol,
                entry.DisplayName,
                entry.AssetClass,
                added.Contains(entry.Symbol),
                entry.SourceSymbol == entry.Symbol ? null : entry.SourceSymbol,
                entry.Exchange))
            .ToList();
        return Result.Success(new SymbolSearchResponse(symbols, page.Value.Total, page.Value.Truncated, true, page.Value.RefreshedAt));
    }
}
