using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Assets;

/// <summary>
/// The figures of a watchlist row (AS6): the last close with its time and the change against the previous
/// daily close. Only delivered, closed candles count; the candle in progress is never used.
/// </summary>
public sealed record RowFigures(double LastClose, long LastCloseTime, double? ChangePercent)
{
    private const long Day = 24 * 60 * 60;

    /// <summary>
    /// Calculates the figures for an asset with a continuous session: its days are cut at 00:00 UTC.
    /// </summary>
    /// <param name="smallest">The smallest timeframe of the asset.</param>
    /// <param name="last">The newest closed candle of that timeframe, or null without candles.</param>
    /// <param name="dailyCandles">The newest daily candles, in any order; empty when the asset has no daily timeframe.</param>
    /// <returns>Null without a candle. The change is null when no daily candle ended before the day of the last close.</returns>
    public static RowFigures? Of(Timeframe smallest, Candle? last, IReadOnlyList<Candle> dailyCandles)
    {
        if (last == null)
        {
            return null;
        }

        // The day the last candle belongs to: the candle from 23:45 to 00:00 still counts for its day.
        var dayStart = last.Value.Time - (last.Value.Time % Day);

        Candle? previousDay = null;
        foreach (var candle in dailyCandles)
        {
            if (candle.Time < dayStart && (previousDay == null || candle.Time > previousDay.Value.Time))
            {
                previousDay = candle;
            }
        }

        double? change = previousDay == null || previousDay.Value.Close == 0
            ? null
            : (last.Value.Close - previousDay.Value.Close) / previousDay.Value.Close * 100;

        return new RowFigures(last.Value.Close, Timeframes.CloseTime(smallest, last.Value.Time), change);
    }
}
