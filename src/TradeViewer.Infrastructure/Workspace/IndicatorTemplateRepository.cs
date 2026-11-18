using System.Globalization;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.Indicators;

namespace TradeViewer.Infrastructure.Workspace;

public sealed class IndicatorTemplateRepository : IIndicatorTemplateRepository
{
    /// <summary>Raised when the shape of the payload changes in a way an older reader cannot ignore. Added fields need no new version.</summary>
    private const int PayloadVersion = 1;

    private readonly WorkspaceDatabase _database;
    private readonly TimeProvider _time;

    public IndicatorTemplateRepository(WorkspaceDatabase database, TimeProvider time)
    {
        _database = database;
        _time = time;
    }

    public async Task<IReadOnlyList<IndicatorTemplate>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand(
            "SELECT id, name, payload_version, payload FROM indicator_templates ORDER BY name COLLATE NOCASE;");

        var templates = new List<IndicatorTemplate>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            // A row this build cannot read (a newer payload, a damaged one) is left out and stays as it is.
            if (!Guid.TryParse(reader.GetString(0), out var id) || reader.GetInt32(2) > PayloadVersion)
            {
                continue;
            }

            var indicators = IndicatorPayload.Read(IndicatorPayload.Deserialize<List<IndicatorPayload.StoredIndicator>>(reader.GetString(3)));
            if (indicators.Count > 0)
            {
                templates.Add(new IndicatorTemplate(id, reader.GetString(1), indicators));
            }
        }

        return templates;
    }

    public async Task SaveAsync(IndicatorTemplate template, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand(
            "INSERT INTO indicator_templates (id, name, payload_version, payload, updated_at) "
            + "VALUES ($id, $name, $version, $payload, $updatedAt) "
            + "ON CONFLICT (id) DO UPDATE SET name = excluded.name, payload_version = excluded.payload_version, "
            + "payload = excluded.payload, updated_at = excluded.updated_at;");
        command.Parameters.AddWithValue("$id", template.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", template.Name);
        command.Parameters.AddWithValue("$version", PayloadVersion);
        command.Parameters.AddWithValue("$payload", IndicatorPayload.Serialize(IndicatorPayload.Write(template.Indicators)));
        command.Parameters.AddWithValue("$updatedAt", _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("DELETE FROM indicator_templates WHERE id = $id;");
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        return command.ExecuteNonQuery() > 0;
    }
}
