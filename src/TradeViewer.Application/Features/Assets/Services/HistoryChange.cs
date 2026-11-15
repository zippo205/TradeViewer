using System.Globalization;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.Assets.Services;

/// <summary>Reads and checks a requested change of the history of an asset.</summary>
public static class HistoryChange
{
    /// <summary>The history of the asset with the requested starts, or a validation error naming the fields.</summary>
    public static Result<AssetHistory> Apply(AssetHistory current, IReadOnlyDictionary<string, string?>? requested, DateOnly today)
    {
        var problems = new Dictionary<string, string[]>();
        var history = current;

        if (requested == null || requested.Count == 0)
        {
            problems["historyFrom"] = new[] { "At least one timeframe is required." };
        }

        foreach (var pair in requested ?? new Dictionary<string, string?>())
        {
            var field = "historyFrom." + pair.Key;

            if (!Timeframes.TryParse(pair.Key, out var timeframe) || !current.Has(timeframe))
            {
                problems[field] = new[] { "The asset has no timeframe " + pair.Key + "." };
                continue;
            }

            if (pair.Value == null)
            {
                history = history.With(timeframe, null);
                continue;
            }

            if (!DateOnly.TryParseExact(pair.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            {
                problems[field] = new[] { "The first day must be a date as yyyy-MM-dd, or null for the complete history." };
            }
            else if (start > today)
            {
                problems[field] = new[] { "The first day must not be in the future." };
            }
            else
            {
                history = history.With(timeframe, start);
            }
        }

        return problems.Count > 0
            ? Error.Validation("asset.history.invalid", "The history is not valid.", problems)
            : Result.Success(history);
    }
}
