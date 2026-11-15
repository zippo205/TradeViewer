using TradeViewer.Domain.Assets;

namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>
/// The symbols that can be added (AS2). Today a fixed list per source that is part of TradeViewer; later the
/// symbol catalog of the Downloader.
/// </summary>
public interface ISymbolCatalog
{
    IReadOnlyList<string> Sources { get; }

    /// <summary>The symbols of a source whose symbol or name contains the text (all without a text), ordered by symbol.</summary>
    IReadOnlyList<SymbolInfo> Search(string source, string? text);

    SymbolInfo? Find(string source, string symbol);
}
