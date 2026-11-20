using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Scenarios;

/// <summary>
/// The snap rules of EWTS's format (SC10), applied by the server to a point the user places or moves (decision 0012,
/// architecture chapter 8). The point always sits on a candle of its level's timeframe. Opening a scenario never
/// snaps anything: a point as written stays as written.
/// </summary>
public static class ScenarioSnap
{
    /// <summary>
    /// The candle of the timeframe that contains the time, or null when there is none (no data, a gap). The candles
    /// are those of that timeframe in time order.
    /// </summary>
    public static Candle? ContainingCandle(IReadOnlyList<Candle> candles, Timeframe timeframe, long time)
    {
        var low = 0;
        var high = candles.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var candle = candles[middle];
            if (time < candle.Time)
            {
                high = middle - 1;
            }
            else if (time >= Timeframes.CloseTime(timeframe, candle.Time))
            {
                low = middle + 1;
            }
            else
            {
                return candle;
            }
        }

        return null;
    }

    /// <summary>
    /// The point on its candle. The given price is where the user let the point go; it is kept for an exit, a stop move
    /// and the stop, and replaced by the candle's low, high or close for the others. For the stop the time is that of
    /// the candle but has no meaning: the file holds only its price.
    /// </summary>
    public static ScenarioPoint Snap(ScenarioPointName name, Candle candle, double givenPrice)
    {
        switch (ScenarioPoints.SnapOf(name))
        {
            case ScenarioSnapKind.Low:
                return new ScenarioPoint(candle.Time, candle.Low);
            case ScenarioSnapKind.High:
                return new ScenarioPoint(candle.Time, candle.High);
            case ScenarioSnapKind.Close:
                return new ScenarioPoint(candle.Time, candle.Close);
            default:
                return new ScenarioPoint(candle.Time, givenPrice);
        }
    }
}
