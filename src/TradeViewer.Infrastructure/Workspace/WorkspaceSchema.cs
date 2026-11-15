using TradeViewer.Infrastructure.Sqlite;

namespace TradeViewer.Infrastructure.Workspace;

/// <summary>The schema scripts of workspace.db (Workspace/Scripts/NNNN_name.sql); an upgrade never loses data (WS4).</summary>
public sealed class WorkspaceSchema : SqliteSchema
{
    public WorkspaceSchema(IEnumerable<SchemaScript> scripts)
        : base("The workspace", scripts)
    {
    }

    public static WorkspaceSchema FromEmbeddedScripts()
    {
        return new WorkspaceSchema(ReadEmbeddedScripts(typeof(WorkspaceSchema).Namespace + ".Scripts."));
    }
}
