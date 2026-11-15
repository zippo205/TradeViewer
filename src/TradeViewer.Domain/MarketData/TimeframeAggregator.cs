namespace TradeViewer.Domain.MarketData;

/// <summary>
/// Builds candles of a timeframe from candles of a smaller one. Today only the current candle (MD8), for
/// assets with a continuous session in UTC; building whole timeframes a source lacks follows (MD3).
/// </summary>
public static class TimeframeAggregator
{
    /// <summary>
    /// The candle in progress of a timeframe: open of the first small candle of its period, high and low over
    /// all, close of the last, volume summed. It can be incomplete when small candles are missing.
    /// </summary>
    /// <param name="timeframe">The timeframe of the chart.</param>
    /// <param name="lastClosedTime">The open time of the newest closed candle the Downloader delivered for it, or null.</param>
    /// <param name="smallest">Closed candles of the smallest timeframe, oldest first, reaching to the newest one.</param>
    /// <returns>
    /// The current candle, or null when there is none: no small candles, or the Downloader has already
    /// delivered the candle of the period the newest small candle belongs to.
    /// </returns>
    public static Candle? CurrentCandle(Timeframe timeframe, long? lastClosedTime, IReadOnlyList<Candle> smallest)
    {
        if (smallest.Count == 0)
        {
            return null;
        }

        // Only the period of the newest small candle: an older period the Downloader has not delivered is not invented (MD1).
        var period = Timeframes.OpenTime(timeframe, smallest[smallest.Count - 1].Time);
        if (lastClosedTime != null && period <= lastClosedTime.Value)
        {
            return null;
        }

        Candle? current = null;
        foreach (var candle in smallest)
        {
            if (candle.Time < period)
            {
                continue;
            }

            current = current == null
                ? candle with { Time = period }
                : current.Value with
                {
                    High = Math.Max(current.Value.High, candle.High),
                    Low = Math.Min(current.Value.Low, candle.Low),
                    Close = candle.Close,
                    Volume = current.Value.Volume + candle.Volume
                };
        }

        return current;
    }
}
