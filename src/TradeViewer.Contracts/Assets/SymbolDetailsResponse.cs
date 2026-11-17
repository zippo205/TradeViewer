namespace TradeViewer.Contracts.Assets;

/// <summary>
/// The answer of GET /api/symbols/{source}/{sourceSymbol}: what the Downloader says about a symbol before it is
/// added (AS2). Session is continuous, exchange or newYorkClose, with its time zone and, for an exchange, its
/// opening hours (HH:mm). Timeframes are those the source provides; Earliest the first date per timeframe code
/// (yyyy-MM-dd), empty without the Downloader. Added: it is an asset of TradeViewer already.
/// </summary>
public sealed record SymbolDetailsResponse(
    string Source,
    string Symbol,
    string SourceSymbol,
    string DisplayName,
    string AssetClass,
    double TickSize,
    string Session,
    string TimeZone,
    string? Open,
    string? Close,
    IReadOnlyList<string> Timeframes,
    IReadOnlyDictionary<string, string> Earliest,
    bool Added);
