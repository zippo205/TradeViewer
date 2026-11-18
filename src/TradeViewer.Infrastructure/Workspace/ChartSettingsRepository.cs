using System.Globalization;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.Charts;

namespace TradeViewer.Infrastructure.Workspace;

public sealed class ChartSettingsRepository : IChartSettingsRepository
{
    /// <summary>Raised when the shape of the payload changes in a way an older reader cannot ignore. Added fields need no new version.</summary>
    private const int PayloadVersion = 1;

    private readonly WorkspaceDatabase _database;
    private readonly TimeProvider _time;

    public ChartSettingsRepository(WorkspaceDatabase database, TimeProvider time)
    {
        _database = database;
        _time = time;
    }

    public async Task<ChartSettings?> GetAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("SELECT payload_version, payload FROM chart_settings WHERE source = $source AND symbol = $symbol;");
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$symbol", symbol);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) > PayloadVersion)
        {
            return null; // nothing saved, or saved by a newer build in a shape this one does not know
        }

        var payload = IndicatorPayload.Deserialize<StoredChartSettings>(reader.GetString(1));
        return payload == null ? null : new ChartSettings(payload.DrawingsHidden, IndicatorPayload.Read(payload.Indicators));
    }

    public async Task<bool> SetAsync(string source, string symbol, ChartSettings settings, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand(
            "INSERT INTO chart_settings (source, symbol, payload_version, payload, updated_at) "
            + "SELECT $source, $symbol, $version, $payload, $updatedAt "
            + "WHERE EXISTS (SELECT 1 FROM assets WHERE source = $source AND symbol = $symbol) "
            + "ON CONFLICT (source, symbol) DO UPDATE SET payload_version = excluded.payload_version, "
            + "payload = excluded.payload, updated_at = excluded.updated_at;");
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$symbol", symbol);
        command.Parameters.AddWithValue("$version", PayloadVersion);
        command.Parameters.AddWithValue("$payload", IndicatorPayload.Serialize(new StoredChartSettings(settings.DrawingsHidden, IndicatorPayload.Write(settings.Indicators))));
        command.Parameters.AddWithValue("$updatedAt", _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));

        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// The stored shape: independent of the Domain record and of the API, so either can change without touching
    /// stored data. Indicators were added with the Indicators epic; a payload without them has none.
    /// </summary>
    private sealed record StoredChartSettings(bool DrawingsHidden, List<IndicatorPayload.StoredIndicator>? Indicators);
}
