using System.Text.Json;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Infrastructure.Workspace;

/// <summary>
/// The default history as the setting "defaultHistory": an object with one entry per timeframe code, the
/// number of days or null for the complete history, e.g. {"15m":90,"1h":365,"4h":730,"1d":null,"1w":null}.
/// </summary>
public sealed class HistorySettingsRepository : IHistorySettingsRepository
{
    public const string Key = "defaultHistory";

    private readonly ISettingsRepository _settings;

    public HistorySettingsRepository(ISettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<HistorySettings> GetAsync(CancellationToken cancellationToken)
    {
        var json = await _settings.GetAsync(Key, cancellationToken);
        if (json == null)
        {
            return HistorySettings.Initial;
        }

        var depths = new Dictionary<Timeframe, HistoryDepth>();
        using var document = JsonDocument.Parse(json);
        foreach (var entry in document.RootElement.EnumerateObject())
        {
            if (Timeframes.TryParse(entry.Name, out var timeframe))
            {
                depths[timeframe] = entry.Value.ValueKind == JsonValueKind.Null
                    ? HistoryDepth.Complete
                    : HistoryDepth.OfDays(entry.Value.GetInt32());
            }
        }

        return new HistorySettings(depths);
    }

    public Task SetAsync(HistorySettings settings, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, int?>();
        foreach (var timeframe in Timeframes.All)
        {
            values[Timeframes.Code(timeframe)] = settings.DepthOf(timeframe).Days;
        }

        return _settings.SetAsync(Key, JsonSerializer.Serialize(values), cancellationToken);
    }
}
