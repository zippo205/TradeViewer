using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Contracts.Charts;

namespace TradeViewer.Infrastructure.Workspace;

public sealed class ChartSettingsRepository : IChartSettingsRepository
{
    /// <summary>Raised when the shape of the payload changes in a way an older reader cannot ignore.</summary>
    private const int PayloadVersion = 1;

    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly WorkspaceDatabase _database;
    private readonly TimeProvider _time;

    public ChartSettingsRepository(WorkspaceDatabase database, TimeProvider time)
    {
        _database = database;
        _time = time;
    }

    public async Task<ChartSettingsResponse?> GetAsync(string source, string symbol, CancellationToken cancellationToken)
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

        return JsonSerializer.Deserialize<ChartSettingsResponse>(reader.GetString(1), Json);
    }

    public async Task<bool> SetAsync(string source, string symbol, ChartSettingsResponse settings, CancellationToken cancellationToken)
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
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(settings, Json));
        command.Parameters.AddWithValue("$updatedAt", _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

        return command.ExecuteNonQuery() > 0;
    }
}
