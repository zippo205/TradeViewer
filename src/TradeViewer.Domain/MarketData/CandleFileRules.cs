using System.Globalization;
using System.Text.RegularExpressions;

namespace TradeViewer.Domain.MarketData;

/// <summary>
/// The rules of the Downloader's candle files (docs/knowledge/downloader-interface.md): the period a file
/// name stands for, and what every row must fulfil. A file that breaks a rule is not imported (MD6).
/// </summary>
public static class CandleFileRules
{
    // <SYMBOL>-<timeframe>-<period>.csv with the period as a day (yyyy-MM-dd), a month (yyyy-MM) or a year (yyyy).
    private static readonly Regex NamePattern = new Regex(
        "^(?<symbol>.+)-(?<timeframe>[0-9]+[mhdwM])-(?<year>[0-9]{4})(-(?<month>[0-9]{2})(-(?<day>[0-9]{2}))?)?\\.csv\\z",
        RegexOptions.CultureInvariant);

    /// <summary>Reads symbol, timeframe and period out of a file name; false when it is not a candle file of a known timeframe.</summary>
    public static bool TryParseName(string? name, out CandleFile? file)
    {
        file = null;
        if (name == null)
        {
            return false;
        }

        var match = NamePattern.Match(name);
        if (!match.Success || !Timeframes.TryParse(match.Groups["timeframe"].Value, out var timeframe))
        {
            return false;
        }

        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        var hasMonth = match.Groups["month"].Success;
        var hasDay = match.Groups["day"].Success;
        var month = hasMonth ? int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture) : 1;
        var day = hasDay ? int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture) : 1;

        if (year < 1970 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        var start = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        var end = hasDay ? start.AddDays(1) : hasMonth ? start.AddMonths(1) : start.AddYears(1);

        file = new CandleFile(name, match.Groups["symbol"].Value, timeframe, ToUnixSeconds(start), ToUnixSeconds(end));
        return true;
    }

    /// <summary>Checks one row against the rules for all sources.</summary>
    /// <param name="candle">The row.</param>
    /// <param name="previousTime">The open time of the row before it, or null for the first row.</param>
    /// <param name="file">The file the row is in.</param>
    /// <returns>Null when the row is valid, otherwise what is wrong with it.</returns>
    public static string? CheckRow(Candle candle, long? previousTime, CandleFile file)
    {
        if (!IsPrice(candle.Open) || !IsPrice(candle.High) || !IsPrice(candle.Low) || !IsPrice(candle.Close))
        {
            return "a price is not greater than zero";
        }

        if (candle.Low > candle.Open || candle.Low > candle.Close)
        {
            return "low is above open or close";
        }

        if (candle.High < candle.Open || candle.High < candle.Close)
        {
            return "high is below open or close";
        }

        if (!(candle.Volume >= 0) || double.IsInfinity(candle.Volume))
        {
            return "volume is negative";
        }

        if (previousTime != null && candle.Time <= previousTime.Value)
        {
            return "open time is not after the row before";
        }

        if (candle.Time < file.PeriodStart || candle.Time >= file.PeriodEnd)
        {
            return "open time lies outside the period of the file";
        }

        return null;
    }

    private static bool IsPrice(double value)
    {
        // Written this way, NaN is not a price either.
        return value > 0 && !double.IsInfinity(value);
    }

    private static long ToUnixSeconds(DateTime utc)
    {
        return (long)(utc - DateTime.UnixEpoch).TotalSeconds;
    }
}
