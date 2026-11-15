// Copied from Downloader.Contracts (Downloader repository). Do not edit; copy again on changes.

namespace Downloader.Contracts.Subscriptions;

/// <summary>Body of PUT /api/v1/clients/{client}/subscriptions/{source}/{symbol}.</summary>
public sealed class SubscriptionRequest
{
    /// <summary>Ticker at the source, e.g. <c>EURUSD=X</c>; default: the symbol of the path.</summary>
    public string? SourceSymbol { get; set; }

    /// <summary>Timeframe codes: 15m, 1h, 4h, 1d, 1w.</summary>
    public List<string>? Timeframes { get; set; }

    /// <summary>Start date for all timeframes (yyyy-MM-dd); default: the configured default history.</summary>
    public DateOnly? HistoryFrom { get; set; }

    /// <summary>Start date per timeframe, overrides <see cref="HistoryFrom"/>.</summary>
    public Dictionary<string, DateOnly>? HistoryFromByTimeframe { get; set; }

    /// <summary>crypto, stock, etf, forex; default: from the source.</summary>
    public string? AssetClass { get; set; }
}
