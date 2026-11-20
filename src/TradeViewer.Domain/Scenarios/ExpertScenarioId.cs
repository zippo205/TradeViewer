using System.Globalization;
using System.Text.RegularExpressions;

namespace TradeViewer.Domain.Scenarios;

/// <summary>
/// The id of an expert scenario, EWTS's rule (SC14): <c>&lt;symbol&gt;-&lt;context tf&gt;-&lt;yyyy-MM&gt;-X&lt;n&gt;</c>, lower case
/// except X, the month of the end of wave 2 when the scenario is first saved, n the next free number of that
/// prefix (btcusdt-1d-2024-03-X1). The id is also the file name without .json, so it is checked strictly: an id
/// that does not match can never name a path outside the folder.
/// </summary>
public static partial class ExpertScenarioId
{
    /// <summary>The context timeframes an expert scenario can have.</summary>
    public static readonly IReadOnlyList<string> ContextTimeframes = new[] { "1d", "4h" };

    /// <summary>Whether the text is an id: lower-case symbol, context timeframe, month, X and a number from 1.</summary>
    public static bool IsValid(string? id)
    {
        return id != null && IdPattern().IsMatch(id);
    }

    /// <summary>The part before the number, e.g. "btcusdt-1d-2024-03-X".</summary>
    public static string Prefix(string symbol, string contextTimeframe, DateTimeOffset wave2End)
    {
        var month = wave2End.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        return symbol.ToLowerInvariant() + "-" + contextTimeframe + "-" + month + "-X";
    }

    /// <summary>The next free id of the prefix: one more than the highest number taken, 1 when none is taken.</summary>
    public static string Next(string prefix, IEnumerable<string> takenIds)
    {
        var highest = 0;
        foreach (var id in takenIds)
        {
            if (!id.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (int.TryParse(id.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > highest)
            {
                highest = number;
            }
        }

        return prefix + (highest + 1).ToString(CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("^[a-z0-9]{1,40}-(1d|4h)-[0-9]{4}-(0[1-9]|1[0-2])-X[1-9][0-9]{0,5}$")]
    private static partial Regex IdPattern();
}
