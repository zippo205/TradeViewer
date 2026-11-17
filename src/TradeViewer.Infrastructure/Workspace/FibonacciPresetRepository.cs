using System.Globalization;
using System.Text.Json;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.Drawings;

namespace TradeViewer.Infrastructure.Workspace;

public sealed class FibonacciPresetRepository : IFibonacciPresetRepository
{
    private const string Columns = "id, name, kind, levels";

    private readonly WorkspaceDatabase _database;
    private readonly TimeProvider _time;

    public FibonacciPresetRepository(WorkspaceDatabase database, TimeProvider time)
    {
        _database = database;
        _time = time;
    }

    public async Task<IReadOnlyList<FibonacciPreset>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("SELECT " + Columns + " FROM fibonacci_presets ORDER BY position;");

        var presets = new List<FibonacciPreset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var preset = ReadRow(reader);
            if (preset != null)
            {
                presets.Add(preset);
            }
        }

        return presets;
    }

    public async Task<FibonacciPreset?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("SELECT " + Columns + " FROM fibonacci_presets WHERE id = $id;");
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRow(reader) : null;
    }

    public async Task SaveAsync(FibonacciPreset preset, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand(
            "INSERT INTO fibonacci_presets (id, name, kind, levels, position, updated_at) "
            + "VALUES ($id, $name, $kind, $levels, COALESCE((SELECT MAX(position) FROM fibonacci_presets), 0) + 1, $updatedAt) "
            + "ON CONFLICT (id) DO UPDATE SET name = excluded.name, kind = excluded.kind, levels = excluded.levels, updated_at = excluded.updated_at;");
        command.Parameters.AddWithValue("$id", preset.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", preset.Name);
        command.Parameters.AddWithValue("$kind", DrawingCodes.Code(preset.Kind));
        command.Parameters.AddWithValue("$levels", StoredLevels.WriteText(preset.Levels));
        command.Parameters.AddWithValue("$updatedAt", _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("DELETE FROM fibonacci_presets WHERE id = $id;");
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        return command.ExecuteNonQuery() > 0;
    }

    /// <returns>The preset, or null when this build cannot read the row (it stays as it is).</returns>
    private static FibonacciPreset? ReadRow(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        if (!Guid.TryParse(reader.GetString(0), out var id) || !DrawingCodes.TryParse(reader.GetString(2), out FibonacciKind kind))
        {
            return null;
        }

        var levels = StoredLevels.ReadText(reader.GetString(3), out var readable);
        return !readable || levels == null ? null : new FibonacciPreset(id, reader.GetString(1), kind, levels);
    }
}
