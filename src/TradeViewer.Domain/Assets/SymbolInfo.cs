using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Assets;

/// <summary>
/// What a source says about one of its symbols. Source and symbol together identify an asset (decision 0003);
/// the symbol is the name of its folder in the Downloader's output. SourceSymbol is the ticker at the source when
/// it differs (yahoo <c>EURUSD=X</c>, tradingview <c>NYSE:BRK.B</c>); null means the same as the symbol, as for
/// binance. Lookups and subscriptions use the <see cref="Ticker"/> (AS2, AS7).
/// </summary>
public sealed record SymbolInfo(
    string Source,
    string Symbol,
    string DisplayName,
    AssetClass AssetClass,
    double TickSize,
    Session Session,
    IReadOnlyList<Timeframe> SourceTimeframes,
    string? SourceSymbol = null)
{
    /// <summary>The ticker at the source: the source symbol, or the symbol when they are the same.</summary>
    public string Ticker => string.IsNullOrEmpty(SourceSymbol) ? Symbol : SourceSymbol;
}
