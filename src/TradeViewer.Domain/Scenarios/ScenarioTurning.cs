using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Scenarios;

/// <summary>
/// EWTS's rule for a turning point on a smaller timeframe (decision 0013, docs/knowledge/ewts-interface.md): the candle
/// whose open time lies inside the context candle of the point and whose low (or high) equals the point's price; on
/// ties the earliest. The candles are those of the smaller timeframe in time order.
/// </summary>
public static class ScenarioTurning
{
    /// <summary>The candle the rule names, or null when none meets it (a gap in the data, or a point that is no extreme).</summary>
    public static Candle? Find(IReadOnlyList<Candle> candles, long from, long to, double price, ScenarioSnapKind kind)
    {
        foreach (var candle in candles)
        {
            if (candle.Time >= from && candle.Time < to && Same(kind == ScenarioSnapKind.Low ? candle.Low : candle.High, price))
            {
                return candle;
            }
        }

        return null;
    }

    /// <summary>
    /// Where the point is shown on the smaller timeframe: the candle of the rule, else the earliest candle with the
    /// lowest low (or highest high) inside the span; null without candles there.
    /// </summary>
    public static Candle? ForView(IReadOnlyList<Candle> candles, long from, long to, double price, ScenarioSnapKind kind)
    {
        var found = Find(candles, from, to, price, kind);
        if (found != null)
        {
            return found;
        }

        Candle? best = null;
        foreach (var candle in candles)
        {
            if (candle.Time < from || candle.Time >= to)
            {
                continue;
            }

            if (best == null || (kind == ScenarioSnapKind.Low ? candle.Low < best.Value.Low : candle.High > best.Value.High))
            {
                best = candle;
            }
        }

        return best;
    }

    private static bool Same(double a, double b)
    {
        return Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
    }
}
