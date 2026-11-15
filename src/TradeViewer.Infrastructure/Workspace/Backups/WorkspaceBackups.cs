using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Contracts.Workspace;
using TradeViewer.Infrastructure.Storage;

namespace TradeViewer.Infrastructure.Workspace.Backups;

/// <summary>
/// Makes, lists, opens and restores the backups of workspace.db (WS3). A restore first checks and upgrades a
/// temporary copy of the backup; only a copy that is ready for work replaces the workspace, so a failed
/// restore changes nothing. There is no safety copy of the replaced workspace.
/// </summary>
public sealed class WorkspaceBackups : IWorkspaceBackups
{
    private const int CopyBufferBytes = 81920;

    private readonly WorkspaceDatabase _database;
    private readonly WorkspaceSchema _schema;
    private readonly BackupStore _store;
    private readonly StorageLocations _locations;
    private readonly WorkspaceRestoreSignal _restored;
    private readonly ILogger<WorkspaceBackups> _logger;

    public WorkspaceBackups(
        WorkspaceDatabase database,
        WorkspaceSchema schema,
        BackupStore store,
        StorageLocations locations,
        WorkspaceRestoreSignal restored,
        ILogger<WorkspaceBackups> logger)
    {
        _restored = restored;
        _database = database;
        _schema = schema;
        _store = store;
        _locations = locations;
        _logger = logger;
    }

    public Task<IReadOnlyList<BackupResponse>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<BackupResponse> backups = _store.List().Select(ToResponse).ToList();
        return Task.FromResult(backups);
    }

    public async Task<BackupResponse> CreateAsync(CancellationToken cancellationToken)
    {
        return ToResponse(await CreateAsync(BackupKind.Manual, cancellationToken));
    }

    public async Task<BackupFile> CreateAsync(BackupKind kind, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        var backup = _store.Create(workspace.Connection, kind, null);

        _logger.LogInformation("Backup {Name} written ({Size} bytes).", backup.Name, backup.SizeBytes);
        return backup;
    }

    public Task<FileContent?> OpenAsync(string name, CancellationToken cancellationToken)
    {
        var path = _store.PathOf(name);
        if (path == null)
        {
            return Task.FromResult<FileContent?>(null);
        }

        try
        {
            // The retention may delete the file while it is downloaded; the open stream stays readable.
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return Task.FromResult<FileContent?>(new FileContent(stream, name, "application/octet-stream"));
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<FileContent?>(null);
        }
    }

    public async Task<RestoreResult> RestoreAsync(string name, CancellationToken cancellationToken)
    {
        var path = _store.PathOf(name);
        if (path == null)
        {
            return new RestoreResult(RestoreStatus.NotFound, "There is no backup named " + name + ".");
        }

        var result = await RestoreFileAsync(path, cancellationToken);
        Log(result, "backup " + name);
        return result;
    }

    public async Task<RestoreResult> RestoreAsync(Stream content, CancellationToken cancellationToken)
    {
        var upload = Path.Combine(_locations.DataPath, "upload-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            if (!await SaveUploadAsync(content, upload, cancellationToken))
            {
                return new RestoreResult(
                    RestoreStatus.TooLarge,
                    "The file is larger than " + (BackupLimits.MaxUploadBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture) + " MB.");
            }

            var result = await RestoreFileAsync(upload, cancellationToken);
            Log(result, "an uploaded file");
            return result;
        }
        finally
        {
            File.Delete(upload);
        }
    }

    /// <summary>Removes what an interrupted restore left behind in the data path.</summary>
    public void DeleteLeftovers()
    {
        foreach (var pattern in new[] { "restore-*.tmp*", "upload-*.tmp" })
        {
            foreach (var file in Directory.EnumerateFiles(_locations.DataPath, pattern))
            {
                File.Delete(file);
            }
        }

        _store.DeleteLeftovers();
    }

    private static async Task<bool> SaveUploadAsync(Stream content, string path, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[CopyBufferBytes];
        long total = 0;

        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return true;
            }

            total += read;
            if (total > BackupLimits.MaxUploadBytes)
            {
                return false;
            }

            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private async Task<RestoreResult> RestoreFileAsync(string source, CancellationToken cancellationToken)
    {
        // The copy lies next to workspace.db, so the final replacement is a rename within one file system.
        var candidate = Path.Combine(_locations.DataPath, "restore-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.Copy(source, candidate);

            var problem = CheckAndUpgrade(candidate);
            if (problem != null)
            {
                return new RestoreResult(RestoreStatus.Invalid, problem);
            }

            using (await _database.Gate.EnterExclusiveAsync(cancellationToken))
            {
                // Nobody uses the workspace now: close it, replace the file, and the next use opens the new one.
                _database.CloseConnections();
                WorkspaceDatabase.DeleteSideFiles(_database.FilePath);
                File.Move(candidate, _database.FilePath, true);
            }

            // The assets may be others now: subscriptions and candle store are brought in line (AS7).
            _restored.Raise();

            return new RestoreResult(RestoreStatus.Restored, "The workspace was restored.");
        }
        finally
        {
            File.Delete(candidate);
            WorkspaceDatabase.DeleteSideFiles(candidate);
        }
    }

    /// <summary>Checks that the file is a sound workspace this build can work with and brings it to the current schema.</summary>
    /// <returns>Null when the file is ready, otherwise what is wrong with it.</returns>
    private string? CheckAndUpgrade(string candidate)
    {
        try
        {
            using var connection = WorkspaceDatabase.OpenFile(candidate);

            if (Scalar(connection, "PRAGMA integrity_check;") != "ok")
            {
                return "The file is damaged.";
            }

            var version = WorkspaceSchema.ReadVersion(connection);
            if (Scalar(connection, "PRAGMA application_id;") != WorkspaceDatabase.ApplicationId.ToString(CultureInfo.InvariantCulture) || version < 1)
            {
                return "The file is not a backup of a TradeViewer workspace.";
            }

            if (_schema.IsNewer(version))
            {
                return _schema.NewerMessage(version);
            }

            Scalar(connection, "PRAGMA journal_mode = WAL;");
            _schema.Apply(connection);
            return null;
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning("A file offered for a restore could not be read as a workspace: {Reason}", ex.Message);
            return "The file is not a SQLite database, or it is damaged.";
        }
    }

    private void Log(RestoreResult result, string source)
    {
        if (result.Status == RestoreStatus.Restored)
        {
            _logger.LogWarning("The workspace was replaced by {Source}.", source);
        }
        else
        {
            _logger.LogWarning("Restore of {Source} refused, nothing changed: {Message}", source, result.Message);
        }
    }

    private static string Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static BackupResponse ToResponse(BackupFile backup)
    {
        var kind = backup.Kind == BackupKind.Daily ? "daily" : backup.Kind == BackupKind.Manual ? "manual" : "before-upgrade";
        return new BackupResponse(backup.Name, backup.CreatedAt, kind, backup.SizeBytes);
    }
}
