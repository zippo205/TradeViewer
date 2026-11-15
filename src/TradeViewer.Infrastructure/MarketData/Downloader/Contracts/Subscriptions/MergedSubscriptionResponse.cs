// Copied from Downloader.Contracts (Downloader repository). Do not edit; copy again on changes.

namespace Downloader.Contracts.Subscriptions;

/// <summary>Items of GET /api/v1/subscriptions: all clients' subscriptions to one (source, symbol).</summary>
public sealed class MergedSubscriptionResponse
{
    public string Source { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public string SourceSymbol { get; set; } = string.Empty;

    public List<string> SubscribedBy { get; set; } = new List<string>();

    /// <summary>Supported timeframes only.</summary>
    public List<MergedTimeframeResponse> Timeframes { get; set; } = new List<MergedTimeframeResponse>();
}

public sealed class MergedTimeframeResponse
{
    public string Timeframe { get; set; } = string.Empty;

    /// <summary>The earliest start of all clients; null = from the first available date.</summary>
    public DateOnly? WantedFrom { get; set; }

    public List<string> SubscribedBy { get; set; } = new List<string>();
}
