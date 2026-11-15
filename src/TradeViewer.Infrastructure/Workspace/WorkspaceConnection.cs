using Microsoft.Data.Sqlite;

namespace TradeViewer.Infrastructure.Workspace;

/// <summary>An open connection to workspace.db together with its shared lease. Dispose it as soon as the work is done.</summary>
public sealed class WorkspaceConnection : IDisposable
{
    private readonly IDisposable _lease;

    public WorkspaceConnection(SqliteConnection connection, IDisposable lease)
    {
        Connection = connection;
        _lease = lease;
    }

    public SqliteConnection Connection { get; }

    public SqliteCommand CreateCommand(string sql)
    {
        var command = Connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    public void Dispose()
    {
        // The connection goes back to the pool first; only then a waiting restore may go on.
        Connection.Dispose();
        _lease.Dispose();
    }
}
