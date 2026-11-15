using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeViewer.Infrastructure.Configuration;
using TradeViewer.Infrastructure.Storage;
using TradeViewer.Infrastructure.Workspace;
using TradeViewer.Infrastructure.Workspace.Backups;

namespace TradeViewer.IntegrationTests.Workspace;

/// <summary>
/// The workspace classes on a temporary data folder, without the web application. <see cref="Start"/> builds a
/// new set of objects on the same folder, like a restart of the application (optionally a newer build with more scripts).
/// </summary>
public sealed class WorkspaceFixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tradeviewer-tests", Guid.NewGuid().ToString("N"));

    public WorkspaceFixture()
    {
        Directory.CreateDirectory(DataPath);
        Locations = new StorageLocations(Path.Combine(_root, "marketdata"), DataPath, null);
    }

    public string DataPath => Path.Combine(_root, "app");

    public StorageLocations Locations { get; }

    public TestClock Clock { get; } = new TestClock();

    public int KeepCount { get; set; } = 14;

    public WorkspaceDatabase Database { get; private set; } = null!;

    public WorkspaceSchema Schema { get; private set; } = null!;

    public BackupStore Store { get; private set; } = null!;

    public WorkspaceBackups Backups { get; private set; } = null!;

    public SettingsRepository Settings { get; private set; } = null!;

    public static SchemaScript ExtraScript(int version, string sql)
    {
        return new SchemaScript(version, version.ToString("0000") + "_test.sql", sql);
    }

    /// <summary>Starts the workspace like the application does; extra scripts stand for a newer build.</summary>
    public int Start(params SchemaScript[] extraScripts)
    {
        Build(extraScripts);
        return new WorkspaceInitializer(Database, Schema, Store, Backups, NullLogger<WorkspaceInitializer>.Instance).Initialize();
    }

    /// <summary>The objects of a build without starting it.</summary>
    public void Build(params SchemaScript[] extraScripts)
    {
        Stop();

        Schema = new WorkspaceSchema(WorkspaceSchema.FromEmbeddedScripts().Scripts.Concat(extraScripts));
        Database = new WorkspaceDatabase(Locations.WorkspaceDatabasePath);
        Store = new BackupStore(Locations.BackupsPath, KeepCount, Clock);
        Backups = new WorkspaceBackups(Database, Schema, Store, Locations, NullLogger<WorkspaceBackups>.Instance);
        Settings = new SettingsRepository(Database, Clock);
    }

    public DailyBackupService DailyService(string time = "03:00")
    {
        var options = new StorageOptions { DataPath = DataPath, Backup = new BackupOptions { KeepCount = KeepCount, Time = time } };
        return new DailyBackupService(Backups, Store, Options.Create(options), Clock, NullLogger<DailyBackupService>.Instance);
    }

    public Task<string?> GetAsync(string key)
    {
        return Settings.GetAsync(key, CancellationToken.None);
    }

    public Task SetAsync(string key, string json)
    {
        return Settings.SetAsync(key, json, CancellationToken.None);
    }

    /// <summary>Reads one value straight from a SQLite file, e.g. a backup.</summary>
    public static string? Scalar(string filePath, string sql)
    {
        using var connection = WorkspaceDatabase.OpenFile(filePath);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The files in the data path apart from the workspace itself: nothing temporary may stay behind.</summary>
    public IReadOnlyList<string> StrayFiles()
    {
        return Directory.EnumerateFiles(DataPath)
            .Select(file => Path.GetFileName(file)!)
            .Where(name => !name.StartsWith(WorkspaceDatabase.FileName, StringComparison.Ordinal))
            .ToList();
    }

    public void Stop()
    {
        if (Database != null)
        {
            Database.CloseConnections();
        }
    }

    public void Dispose()
    {
        Stop();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
