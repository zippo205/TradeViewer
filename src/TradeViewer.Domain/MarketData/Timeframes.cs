namespace TradeViewer.Domain.MarketData;

/// <summary>Codes, durations and candle boundaries of the timeframes. All times are UTC, as Unix seconds.</summary>
public static class Timeframes
{
    private const long Minute = 60;
    private const long Hour = 60 * Minute;
    private const long Day = 24 * Hour;
    private const long Week = 7 * Day;

    /// <summary>1970-01-05, the first Monday of Unix time: weekly candles open on a Monday at 00:00 UTC.</summary>
    private const long FirstMonday = 4 * Day;

    /// <summary>All timeframes, smallest first.</summary>
    public static IReadOnlyList<Timeframe> All { get; } = new[] { Timeframe.M15, Timeframe.H1, Timeframe.H4, Timeframe.D1, Timeframe.W1 };

    public static string Code(Timeframe timeframe)
    {
        switch (timeframe)
        {
            case Timeframe.M15:
                return "15m";
            case Timeframe.H1:
                return "1h";
            case Timeframe.H4:
                return "4h";
            case Timeframe.D1:
                return "1d";
            case Timeframe.W1:
                return "1w";
            default:
                throw new ArgumentOutOfRangeException(nameof(timeframe));
        }
    }

    public static bool TryParse(string? code, out Timeframe timeframe)
    {
        foreach (var candidate in All)
        {
            if (Code(candidate) == code)
            {
                timeframe = candidate;
                return true;
            }
        }

        timeframe = default;
        return false;
    }

    /// <summary>The length of one candle in seconds.</summary>
    public static long Seconds(Timeframe timeframe)
    {
        switch (timeframe)
        {
            case Timeframe.M15:
                return 15 * Minute;
            case Timeframe.H1:
                return Hour;
            case Timeframe.H4:
                return 4 * Hour;
            case Timeframe.D1:
                return Day;
            case Timeframe.W1:
                return Week;
            default:
                throw new ArgumentOutOfRangeException(nameof(timeframe));
        }
    }

    /// <summary>
    /// The open time of the candle that contains the time, for a continuous session in UTC (crypto):
    /// intraday candles count from midnight, a daily candle opens at 00:00, a weekly one on Monday 00:00.
    /// </summary>
    public static long OpenTime(Timeframe timeframe, long time)
    {
        var offset = timeframe == Timeframe.W1 ? FirstMonday : 0;
        var seconds = Seconds(timeframe);
        return FloorDivide(time - offset, seconds) * seconds + offset;
    }

    /// <summary>The time the candle that opened at the open time closes; also the open time of the next one.</summary>
    public static long CloseTime(Timeframe timeframe, long openTime)
    {
        return openTime + Seconds(timeframe);
    }

    private static long FloorDivide(long value, long divisor)
    {
        var quotient = value / divisor;
        return value % divisor < 0 ? quotient - 1 : quotient;
    }
}
