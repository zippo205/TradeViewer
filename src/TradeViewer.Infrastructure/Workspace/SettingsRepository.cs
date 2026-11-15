using System.Globalization;
using TradeViewer.Application.Abstractions.Workspace;

namespace TradeViewer.Infrastructure.Workspace;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly WorkspaceDatabase _database;
    private readonly TimeProvider _time;

    public SettingsRepository(WorkspaceDatabase database, TimeProvider time)
    {
        _database = database;
        _time = time;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("SELECT value FROM settings WHERE key = $key;");
        command.Parameters.AddWithValue("$key", key);

        return command.ExecuteScalar() as string;
    }

    public async Task SetAsync(string key, string json, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand(
            "INSERT INTO settings (key, value, updated_at) VALUES ($key, $value, $updatedAt) "
            + "ON CONFLICT (key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;");
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", json);
        command.Parameters.AddWithValue("$updatedAt", _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }
}
