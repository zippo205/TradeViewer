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
