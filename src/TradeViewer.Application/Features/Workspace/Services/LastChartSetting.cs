using System.Text.Json;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Contracts.Workspace;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Features.Workspace.Services;

/// <summary>The chart last used, kept as the setting "lastChart" (CP3). With several windows the last one opened wins (WS1).</summary>
public static class LastChartSetting
{
    public const string Key = "lastChart";

    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    public static async Task<LastChartResponse?> ReadAsync(ISettingsRepository settings, CancellationToken cancellationToken)
    {
        var json = await settings.GetAsync(Key, cancellationToken);
        return json == null ? null : JsonSerializer.Deserialize<LastChartResponse>(json, Json);
    }

    public static Task WriteAsync(ISettingsRepository settings, LastChartResponse chart, CancellationToken cancellationToken)
    {
        return settings.SetAsync(Key, JsonSerializer.Serialize(chart, Json), cancellationToken);
    }

    /// <returns>Null when the chart can be remembered, otherwise the error naming the fields.</returns>
    public static Error? Validate(LastChartResponse chart)
    {
        var problems = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(chart.Source))
        {
            problems["lastChart.source"] = new[] { "The source is required." };
        }

        if (string.IsNullOrWhiteSpace(chart.Symbol))
        {
            problems["lastChart.symbol"] = new[] { "The symbol is required." };
        }

        if (!Timeframes.TryParse(chart.Timeframe, out _))
        {
            problems["lastChart.timeframe"] = new[] { "The timeframe must be one of 15m, 1h, 4h, 1d, 1w." };
        }

        return problems.Count == 0 ? null : Error.Validation("settings.invalid", "The settings are not valid.", problems);
    }
}
