using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Web.Charting;

namespace TradeViewer.Web.Scenarios;

/// <summary>A level whose stored candles do not cover the span of a scenario, and why.</summary>
public sealed record ScenarioGap(string Level, string Timeframe, string Reason);

/// <summary>
/// SC6: whether the stored candles of the three levels cover the span of a scenario once the history is loaded. A
/// span that starts before the first candle or ends after the last one (up to now) cannot be edited, and a new
/// scenario with it is not created. One candle of slack at both ends; an asset with exchange hours gets four days
/// more, for weekends and holidays.
/// </summary>
public static class ScenarioCoverage
{
    private static readonly TimeSpan ClosedDays = TimeSpan.FromDays(4);

    public static IReadOnlyList<ScenarioGap> Missing(
        AssetResponse asset, IReadOnlyList<ScenarioLevelRecord> levels, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
    {
        var gaps = new List<ScenarioGap>();
        var end = to < now ? to : now;
        var closed = string.Equals(asset.AssetClass, "crypto", StringComparison.OrdinalIgnoreCase) ? TimeSpan.Zero : ClosedDays;
        foreach (var level in levels)
        {
            var stored = asset.Timeframes.FirstOrDefault(timeframe => timeframe.Timeframe == level.Timeframe);
            var candle = TimeSpan.FromSeconds(TimeframeChoice.Seconds(level.Timeframe) ?? 86400);
            var slack = candle + closed;
            if (stored?.FirstCandle == null || stored.LastCandle == null)
            {
                gaps.Add(new ScenarioGap(level.Level, level.Timeframe, "no candles are stored"));
            }
            else if (stored.FirstCandle.Value > from + slack)
            {
                gaps.Add(new ScenarioGap(level.Level, level.Timeframe, "the candles start on " + Day(stored.FirstCandle.Value) + ", after the start of the span"));
            }
            else if (stored.LastCandle.Value < end - slack - candle)
            {
                gaps.Add(new ScenarioGap(level.Level, level.Timeframe, "the candles end on " + Day(stored.LastCandle.Value) + ", before the end of the span"));
            }
        }

        return gaps;
    }

    /// <summary>The gaps as one sentence for the page.</summary>
    public static string Text(IReadOnlyList<ScenarioGap> gaps)
    {
        return string.Join("; ", gaps.Select(gap => gap.Level + " " + gap.Timeframe + ": " + gap.Reason)) + ".";
    }

    private static string Day(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
