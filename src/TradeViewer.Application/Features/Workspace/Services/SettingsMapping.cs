using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Workspace;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.Workspace.Services;

/// <summary>Maps the settings between their records and the models, and checks what the user sent.</summary>
public static class SettingsMapping
{
    /// <summary>Ten years more than any source has: a larger number is a typing mistake.</summary>
    public const int MaxHistoryDays = 36500;

    public static SettingsResponse ToResponse(HistorySettings history, LastChartResponse? lastChart)
    {
        var days = new Dictionary<string, int?>();
        foreach (var timeframe in Timeframes.All)
        {
            days[Timeframes.Code(timeframe)] = history.DepthOf(timeframe).Days;
        }

        return new SettingsResponse(days, lastChart);
    }

    public static Result<HistorySettings> ToHistory(IReadOnlyDictionary<string, int?> defaultHistory)
    {
        var problems = new Dictionary<string, string[]>();
        var depths = new Dictionary<Timeframe, HistoryDepth>();

        foreach (var pair in defaultHistory)
        {
            var field = "defaultHistory." + pair.Key;
            if (!Timeframes.TryParse(pair.Key, out var timeframe))
            {
                problems[field] = new[] { pair.Key + " is not a timeframe." };
            }
            else if (pair.Value != null && (pair.Value < 1 || pair.Value > MaxHistoryDays))
            {
                problems[field] = new[] { "The days must be between 1 and " + MaxHistoryDays + ", or null for the complete history." };
            }
            else
            {
                depths[timeframe] = pair.Value == null ? HistoryDepth.Complete : HistoryDepth.OfDays(pair.Value.Value);
            }
        }

        return problems.Count > 0
            ? Error.Validation("settings.invalid", "The settings are not valid.", problems)
            : Result.Success(new HistorySettings(depths));
    }
}
