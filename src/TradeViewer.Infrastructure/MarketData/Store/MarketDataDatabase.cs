using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TradeViewer.Infrastructure.Sqlite;

namespace TradeViewer.Infrastructure.MarketData.Store;

/// <summary>
/// marketdata.db in the data path: the candle store, a cache (decision 0002). WAL mode with
/// synchronous=NORMAL: a power loss can lose the last import, which the rescan repeats. A store that cannot
/// be used is set aside and a new one is filled from the files; it never stops the start.
/// </summary>
public sealed class MarketDataDatabase
{
    public const string FileName = "marketdata.db";

    private readonly string _connectionString;
    private readonly MarketDataSchema _schema;
    private readonly TimeProvider _time;
    private readonly ILogger<MarketDataDatabase> _logger;

    public MarketDataDatabase(string filePath, MarketDataSchema schema, TimeProvider time, ILogger<MarketDataDatabase> logger)
    {
        FilePath = filePath;
        _schema = schema;
        _time = time;
        _logger = logger;

        var builder = new SqliteConnectionStringBuilder();
        builder.DataSource = filePath;
        builder.Mode = SqliteOpenMode.ReadWriteCreate;
        builder.Cache = SqliteCacheMode.Private;
        builder.Pooling = true;
        _connectionString = builder.ToString();
    }

    public string FilePath { get; }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>Creates or upgrades the store at the start.</summary>
    /// <returns>True when an unusable store was set aside and an empty one was created.</returns>
    public bool Initialize()
    {
        var existed = File.Exists(FilePath);
        try
        {
            Prepare();
            return false;
        }
        catch (Exception ex) when (existed && (ex is SqliteException || ex is InvalidOperationException))
        {
            var aside = SetAside();
            _logger.LogWarning(
                "The candle store could not be used ({Reason}). It was renamed to {Aside}; a new store is filled from the Downloader's files.",
                ex.Message,
                aside);

            Prepare();
            return true;
        }
    }

    private void Prepare()
    {
        using var connection = Open();

        var version = SqliteSchema.ReadVersion(connection);
        if (_schema.IsNewer(version))
        {
            throw new InvalidOperationException(_schema.NewerMessage(version));
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA journal_mode = WAL;";
            command.ExecuteNonQuery();
        }

        _schema.Apply(connection);
    }

    private string SetAside()
    {
        using (var connection = new SqliteConnection(_connectionString))
        {
            SqliteConnection.ClearPool(connection);
        }

        var stamp = _time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var aside = FilePath + ".broken-" + stamp;
        File.Move(FilePath, aside, true);
        File.Delete(FilePath + "-wal");
        File.Delete(FilePath + "-shm");
        return aside;
    }
}
