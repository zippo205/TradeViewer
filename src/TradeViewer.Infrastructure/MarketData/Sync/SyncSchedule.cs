using TradeViewer.Domain.MarketData;

namespace TradeViewer.Infrastructure.MarketData.Sync;

/// <summary>
/// When the Downloader is expected to have written new candles of an asset: after every close of the asset's
/// smallest timeframe, 30 seconds later (its update cycle starts at :00:30, :15:30, ... UTC) (MD2).
/// </summary>
public static class SyncSchedule
{
    public static readonly TimeSpan CycleOffset = TimeSpan.FromSeconds(30);

    /// <summary>The latest due time that is not after now.</summary>
    public static DateTimeOffset LastDue(DateTimeOffset now, Timeframe smallest)
    {
        var shifted = now.ToUnixTimeSeconds() - (long)CycleOffset.TotalSeconds;
        var close = Timeframes.OpenTime(smallest, shifted);
        return DateTimeOffset.FromUnixTimeSeconds(close) + CycleOffset;
    }

    /// <summary>The first due time after now.</summary>
    public static DateTimeOffset NextDue(DateTimeOffset now, Timeframe smallest)
    {
        return LastDue(now, smallest).AddSeconds(Timeframes.Seconds(smallest));
    }
}
