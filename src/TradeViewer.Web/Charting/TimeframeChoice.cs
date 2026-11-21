namespace TradeViewer.Web.Charting;

/// <summary>Which timeframe a chart shows when another asset is chosen (CP1, decision 0008).</summary>
public static class TimeframeChoice
{
    /// <summary>The timeframes TradeViewer knows, smallest first; the same as the Domain's.</summary>
    public static readonly IReadOnlyList<string> Order = new[] { "15m", "1h", "4h", "1d", "1w" };

    /// <summary>
    /// The current timeframe when the asset has it; otherwise the next larger one it has; without one the largest
    /// smaller one. Null when the asset has none. Available: the timeframes of the asset that can be charted.
    /// </summary>
    public static string? For(string current, IReadOnlyCollection<string> available)
    {
        var known = Order.Where(available.Contains).ToList();
        if (known.Count == 0)
        {
            return null;
        }

        if (known.Contains(current))
        {
            return current;
        }

        var position = IndexOf(current);
        var larger = known.FirstOrDefault(timeframe => IndexOf(timeframe) > position);
        return larger ?? known[known.Count - 1];
    }

    /// <summary>The length of a candle in seconds, null for a code TradeViewer does not know.</summary>
    public static long? Seconds(string timeframe)
    {
        return timeframe switch
        {
            "15m" => 15 * 60,
            "1h" => 3600,
            "4h" => 4 * 3600,
            "1d" => 86400,
            "1w" => 7 * 86400,
            _ => null
        };
    }

    /// <summary>
    /// The window to show after the timeframe changed on the scenario pages (SC2): the time in the middle stays in the
    /// middle and about as many candles stay in view, so a lower timeframe zooms in and a higher one out. Times in Unix
    /// seconds; null when a timeframe is unknown.
    /// </summary>
    public static (long From, long To)? CentredWindow(long from, long to, string before, string after)
    {
        if (Seconds(before) is not long old || Seconds(after) is not long next)
        {
            return null;
        }

        var middle = from + (to - from) / 2;
        var half = (long)((to - from) / 2.0 * next / old);
        half = Math.Max(half, 5 * next); // at least ten candles
        return (middle - half, middle + half);
    }

    private static int IndexOf(string timeframe)
    {
        for (var i = 0; i < Order.Count; i++)
        {
            if (Order[i] == timeframe)
            {
                return i;
            }
        }

        return -1;
    }
}
