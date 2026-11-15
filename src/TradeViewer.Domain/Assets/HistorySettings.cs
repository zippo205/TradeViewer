using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Assets;

/// <summary>The default history per timeframe, which the user sets once (AS3). A timeframe that is not named gets the complete history.</summary>
public sealed class HistorySettings
{
    private readonly IReadOnlyDictionary<Timeframe, HistoryDepth> _depths;

    public HistorySettings(IReadOnlyDictionary<Timeframe, HistoryDepth> depths)
    {
        _depths = depths;
    }

    /// <summary>The first setting: 1d and 1w complete, 4h two years, 1h one year, 15m 90 days.</summary>
    public static HistorySettings Initial { get; } = new HistorySettings(new Dictionary<Timeframe, HistoryDepth>
    {
        [Timeframe.M15] = HistoryDepth.OfDays(90),
        [Timeframe.H1] = HistoryDepth.OfDays(365),
        [Timeframe.H4] = HistoryDepth.OfDays(730),
        [Timeframe.D1] = HistoryDepth.Complete,
        [Timeframe.W1] = HistoryDepth.Complete
    });

    public HistoryDepth DepthOf(Timeframe timeframe)
    {
        return _depths.TryGetValue(timeframe, out var depth) ? depth : HistoryDepth.Complete;
    }

    /// <summary>The history of an asset that is added today with these timeframes: the start dates are fixed on that day.</summary>
    public AssetHistory StartingAt(DateOnly today, IEnumerable<Timeframe> timeframes)
    {
        var starts = new Dictionary<Timeframe, DateOnly?>();
        foreach (var timeframe in timeframes)
        {
            var depth = DepthOf(timeframe);
            starts[timeframe] = depth.IsComplete ? null : today.AddDays(-depth.Days!.Value);
        }

        return new AssetHistory(starts);
    }
}
