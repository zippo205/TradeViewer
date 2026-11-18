namespace TradeViewer.Domain.Indicators;

/// <summary>
/// How many candles before the first wanted one an indicator needs, so that a page calculated on its own
/// gives the values of a calculation over the whole history. SMA and BB look back a fixed n - 1 candles.
/// EMA and RMA never forget their start; they get as many candles as make its influence smaller than
/// <see cref="Tolerance"/> of the value, so two pages meet without a visible step.
/// </summary>
public static class WarmUp
{
    /// <summary>The share of the starting value a smoothed value may still contain at the first wanted candle.</summary>
    public const double Tolerance = 1e-10;

    public static int Candles(IndicatorParameters parameters) => parameters switch
    {
        SmaParameters p => p.Length - 1,
        BollingerBandsParameters p => p.Length - 1,
        EmaParameters p => Smoothing(p.Length, 2.0 / (p.Length + 1)),
        RsiParameters p => 1 + Smoothing(p.Length, 1.0 / p.Length), // one more candle: the first has no change
        AtrParameters p => 1 + Smoothing(p.Length, 1.0 / p.Length), // one more candle: the true range needs the close before
        MacdParameters p => Smoothing(Math.Max(p.Fast, p.Slow), 2.0 / (Math.Max(p.Fast, p.Slow) + 1)) + Smoothing(p.Signal, 2.0 / (p.Signal + 1)),
        null => throw new ArgumentNullException(nameof(parameters)),
        _ => throw new ArgumentOutOfRangeException(nameof(parameters))
    };

    /// <summary>The seed (n - 1 candles) plus the candles after which (1 - alpha)^k is below the tolerance.</summary>
    private static int Smoothing(int length, double alpha)
    {
        if (alpha >= 1)
        {
            return length - 1; // length 1: the value is the price itself
        }

        var decay = (int)Math.Ceiling(Math.Log(Tolerance) / Math.Log(1 - alpha));
        return length - 1 + decay;
    }
}
