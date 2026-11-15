using Microsoft.Data.Sqlite;

namespace TradeViewer.Infrastructure.Workspace;

/// <summary>
/// workspace.db in the data path: everything the user creates (decision 0002). WAL mode with synchronous=FULL,
/// so a committed change survives a power loss. Connections are pooled by Microsoft.Data.Sqlite.
/// </summary>
public sealed class WorkspaceDatabase
{
    public const string FileName = "workspace.db";

    /// <summary>"TVWS": written into the file header by script 0001; a restore refuses any other SQLite file.</summary>
    public const int ApplicationId = 0x54565753;

    private readonly string _connectionString;

    public WorkspaceDatabase(string filePath)
    {
        FilePath = filePath;
        _connectionString = ConnectionString(filePath, SqliteOpenMode.ReadWriteCreate, true);
    }

    public string FilePath { get; }

    public WorkspaceGate Gate { get; } = new WorkspaceGate();

    /// <summary>The way every repository reaches the database: a shared lease and a connection, returned together.</summary>
    public async Task<WorkspaceConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var lease = await Gate.EnterAsync(cancellationToken);
        try
        {
            return new WorkspaceConnection(OpenConnection(), lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    /// <summary>A connection without a lease; only for the start and the restore, which hold the exclusive lease.</summary>
    public SqliteConnection OpenConnection()
    {
        return Open(_connectionString);
    }

    /// <summary>Closes the idle connections of the pool, so the file can be replaced. The caller holds the exclusive lease.</summary>
    public void CloseConnections()
    {
        using var connection = new SqliteConnection(_connectionString);
        SqliteConnection.ClearPool(connection);
    }

    /// <summary>Opens an existing SQLite file (a backup to check) without pooling: closing the connection releases the file.</summary>
    public static SqliteConnection OpenFile(string filePath)
    {
        return Open(ConnectionString(filePath, SqliteOpenMode.ReadWrite, false));
    }

    /// <summary>The files SQLite keeps next to a database in WAL mode.</summary>
    public static void DeleteSideFiles(string filePath)
    {
        File.Delete(filePath + "-wal");
        File.Delete(filePath + "-shm");
    }

    private static SqliteConnection Open(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA synchronous = FULL; PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static string ConnectionString(string filePath, SqliteOpenMode mode, bool pooling)
    {
        var builder = new SqliteConnectionStringBuilder();
        builder.DataSource = filePath;
        builder.Mode = mode;
        builder.Cache = SqliteCacheMode.Private;
        builder.Pooling = pooling;
        return builder.ToString();
    }
}
