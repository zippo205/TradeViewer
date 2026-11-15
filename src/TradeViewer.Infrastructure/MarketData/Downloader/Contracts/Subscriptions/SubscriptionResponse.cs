// Copied from Downloader.Contracts (Downloader repository). Do not edit; copy again on changes.

using System.Text.Json.Serialization;

namespace Downloader.Contracts.Subscriptions;

/// <summary>A client's subscription: response of PUT and items of GET /api/v1/clients/{client}/subscriptions.</summary>
public sealed class SubscriptionResponse
{
    public string Source { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public string SourceSymbol { get; set; } = string.Empty;

    /// <summary>crypto, stock, etf, forex; null if not known yet.</summary>
    public string? AssetClass { get; set; }

    /// <summary>The timeframes the source provides; clients aggregate the others.</summary>
    public List<string> SourceTimeframes { get; set; } = new List<string>();

    public List<TimeframeSubscriptionResponse> Timeframes { get; set; } = new List<TimeframeSubscriptionResponse>();
}

public sealed class TimeframeSubscriptionResponse
{
    public string Timeframe { get; set; } = string.Empty;

    /// <summary>accepted, unsupported</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Effective start (yyyy-MM-dd); not set for unsupported timeframes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? HistoryFrom { get; set; }

    /// <summary>True: the requested start lay before what the source provides.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Clipped { get; set; }
}
