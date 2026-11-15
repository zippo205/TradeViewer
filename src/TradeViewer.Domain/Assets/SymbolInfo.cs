using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Assets;

/// <summary>What a source says about one of its symbols. Source and symbol together identify an asset (decision 0003).</summary>
public sealed record SymbolInfo(
    string Source,
    string Symbol,
    string DisplayName,
    AssetClass AssetClass,
    double TickSize,
    Session Session,
    IReadOnlyList<Timeframe> SourceTimeframes);
