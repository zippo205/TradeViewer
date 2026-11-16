using System.Globalization;

namespace TradeViewer.Web.Charting;

/// <summary>How figures are written on the pages. The figures themselves come from the Api; nothing is calculated here.</summary>
public static class Figures
{
    /// <summary>A price with the precision of the tick size of its asset (0.01 -> two decimals).</summary>
    public static string Price(double value, double tickSize)
    {
        var decimals = 0;
        var scaled = tickSize > 0 && double.IsFinite(tickSize) ? tickSize : 0.01;
        while (decimals < 10 && Math.Abs(scaled - Math.Round(scaled)) > 1e-9)
        {
            scaled *= 10;
            decimals++;
        }

        return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
    }

    /// <summary>A change in percent with its sign: +3.13%, -0.50%.</summary>
    public static string Percent(double value)
    {
        return (value >= 0 ? "+" : string.Empty) + value.ToString("F2", CultureInfo.InvariantCulture) + "%";
    }

    public static string TimeUtc(DateTimeOffset time)
    {
        return time.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
    }
}
