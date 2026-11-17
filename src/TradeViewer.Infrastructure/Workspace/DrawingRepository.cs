using System.Globalization;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.Drawings;

namespace TradeViewer.Infrastructure.Workspace;

public sealed class DrawingRepository : IDrawingRepository
{
    private const string Columns = "id, source, symbol, tool, payload_version, payload";

    private readonly WorkspaceDatabase _database;
    private readonly TimeProvider _time;

    public DrawingRepository(WorkspaceDatabase database, TimeProvider time)
    {
        _database = database;
        _time = time;
    }

    public async Task<IReadOnlyList<Drawing>> GetByAssetAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("SELECT " + Columns + " FROM drawings WHERE source = $source AND symbol = $symbol ORDER BY seq;");
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$symbol", symbol);

        var drawings = new List<Drawing>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var drawing = ReadRow(reader);
            if (drawing != null)
            {
                drawings.Add(drawing);
            }
        }

        return drawings;
    }

    public async Task<Drawing?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("SELECT " + Columns + " FROM drawings WHERE id = $id;");
        command.Parameters.AddWithValue("$id", Key(id));

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRow(reader) : null;
    }

    public async Task<DrawingSaveResult> SaveAsync(Drawing drawing, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var transaction = workspace.Connection.BeginTransaction();

        using (var owner = workspace.CreateCommand("SELECT source, symbol FROM drawings WHERE id = $id;"))
        {
            owner.Transaction = transaction;
            owner.Parameters.AddWithValue("$id", Key(drawing.Id));
            using var reader = owner.ExecuteReader();
            if (reader.Read() && (reader.GetString(0) != drawing.Source || reader.GetString(1) != drawing.Symbol))
            {
                return DrawingSaveResult.OtherAsset;
            }
        }

        // A new drawing goes on top (seq); a saved one keeps its place. The asset must exist.
        using (var upsert = workspace.CreateCommand(
            "INSERT INTO drawings (id, source, symbol, tool, payload_version, payload, seq, updated_at) "
            + "SELECT $id, $source, $symbol, $tool, $version, $payload, COALESCE((SELECT MAX(seq) FROM drawings), 0) + 1, $updatedAt "
            + "WHERE EXISTS (SELECT 1 FROM assets WHERE source = $source AND symbol = $symbol) "
            + "ON CONFLICT (id) DO UPDATE SET tool = excluded.tool, payload_version = excluded.payload_version, "
            + "payload = excluded.payload, updated_at = excluded.updated_at;"))
        {
            upsert.Transaction = transaction;
            upsert.Parameters.AddWithValue("$id", Key(drawing.Id));
            upsert.Parameters.AddWithValue("$source", drawing.Source);
            upsert.Parameters.AddWithValue("$symbol", drawing.Symbol);
            upsert.Parameters.AddWithValue("$tool", DrawingCodes.Code(drawing.Tool));
            upsert.Parameters.AddWithValue("$version", drawing.Version);
            upsert.Parameters.AddWithValue("$payload", DrawingPayload.Write(drawing));
            upsert.Parameters.AddWithValue("$updatedAt", _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));

            if (upsert.ExecuteNonQuery() == 0)
            {
                return DrawingSaveResult.UnknownAsset;
            }
        }

        transaction.Commit();
        return DrawingSaveResult.Saved;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("DELETE FROM drawings WHERE id = $id;");
        command.Parameters.AddWithValue("$id", Key(id));

        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>Ids are stored in the lower-case form with hyphens, as Guid.ToString() writes them.</summary>
    private static string Key(Guid id)
    {
        return id.ToString("D");
    }

    private static Drawing? ReadRow(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        if (!Guid.TryParse(reader.GetString(0), out var id))
        {
            return null;
        }

        return DrawingPayload.Read(id, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetString(5));
    }
}
