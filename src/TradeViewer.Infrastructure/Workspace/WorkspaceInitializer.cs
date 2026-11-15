using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradeViewer.Infrastructure.Workspace.Backups;

namespace TradeViewer.Infrastructure.Workspace;

/// <summary>
/// Opens workspace.db at the start: creates it when missing, refuses a damaged file and a file of a newer
/// version, backs it up before a schema upgrade and applies the new scripts (WS2, WS4).
/// </summary>
public sealed class WorkspaceInitializer : IHostedService
{
    private readonly WorkspaceDatabase _database;
    private readonly WorkspaceSchema _schema;
    private readonly BackupStore _store;
    private readonly WorkspaceBackups _backups;
    private readonly ILogger<WorkspaceInitializer> _logger;

    public WorkspaceInitializer(
        WorkspaceDatabase database,
        WorkspaceSchema schema,
        BackupStore store,
        WorkspaceBackups backups,
        ILogger<WorkspaceInitializer> logger)
    {
        _database = database;
        _schema = schema;
        _store = store;
        _backups = backups;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using (await _database.Gate.EnterExclusiveAsync(cancellationToken))
        {
            Initialize();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <returns>The schema version of the workspace.</returns>
    public int Initialize()
    {
        _backups.DeleteLeftovers();

        if (File.Exists(_database.FilePath))
        {
            CheckIntegrity();
        }

        using var connection = _database.OpenConnection();

        var current = WorkspaceSchema.ReadVersion(connection);
        if (_schema.IsNewer(current))
        {
            throw new InvalidOperationException(_schema.NewerMessage(current) + " File: " + _database.FilePath);
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA journal_mode = WAL;";
            command.ExecuteNonQuery();
        }

        if (current > 0 && current < _schema.LatestVersion)
        {
            var backup = _store.Create(connection, BackupKind.BeforeUpgrade, _schema.LatestVersion);
            _logger.LogInformation(
                "The workspace is upgraded from schema version {From} to {To}; backup before: {Backup}.",
                current,
                _schema.LatestVersion,
                backup.Name);
        }

        var version = _schema.Apply(connection);
        _logger.LogInformation("Workspace {Path}, schema version {Version}.", _database.FilePath, version);
        return version;
    }

    /// <summary>On damage TradeViewer stops and changes nothing: the owner decides which backup to take.</summary>
    private void CheckIntegrity()
    {
        string result;
        try
        {
            using var connection = _database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
        {
            result = ex.Message;
        }

        if (result != "ok")
        {
            _database.CloseConnections();
            throw new InvalidOperationException(
                "The workspace " + _database.FilePath + " is damaged (PRAGMA quick_check: " + result + "). TradeViewer stops and changes nothing. "
                + "Put a backup from " + _store.Folder + " in its place (docs/runbooks/deploy.md, section D).");
        }
    }
}
