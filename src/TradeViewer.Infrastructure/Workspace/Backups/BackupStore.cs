using Microsoft.Data.Sqlite;

namespace TradeViewer.Infrastructure.Workspace.Backups;

/// <summary>The backups folder: writes a backup with the SQLite online backup API, lists the backups and keeps the newest (WS3).</summary>
public sealed class BackupStore
{
    private readonly object _sync = new object();
    private readonly int _keepCount;
    private readonly TimeProvider _time;

    public BackupStore(string folder, int keepCount, TimeProvider time)
    {
        Folder = folder;
        _keepCount = keepCount;
        _time = time;
    }

    public string Folder { get; }

    /// <summary>
    /// Copies the database of the connection into a new backup file, while others read and write it. The file
    /// appears under its name only when it is complete. Afterwards the backups beyond the keep count are deleted.
    /// </summary>
    public BackupFile Create(SqliteConnection source, BackupKind kind, int? upgradeVersion)
    {
        lock (_sync)
        {
            Directory.CreateDirectory(Folder);

            // Names are to the second; a second backup in the same second gets the next one.
            var now = _time.GetUtcNow();
            var createdAt = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero);
            var name = BackupFileName.Create(createdAt, kind, upgradeVersion);
            while (File.Exists(Path.Combine(Folder, name)))
            {
                createdAt = createdAt.AddSeconds(1);
                name = BackupFileName.Create(createdAt, kind, upgradeVersion);
            }

            var path = Path.Combine(Folder, name);
            var temporary = path + ".tmp";
            DeleteTemporary(temporary);

            try
            {
                var builder = new SqliteConnectionStringBuilder();
                builder.DataSource = temporary;
                builder.Pooling = false;

                using (var destination = new SqliteConnection(builder.ToString()))
                {
                    destination.Open();
                    source.BackupDatabase(destination);
                }

                WorkspaceDatabase.DeleteSideFiles(temporary);
                File.Move(temporary, path);
            }
            catch
            {
                DeleteTemporary(temporary);
                throw;
            }

            foreach (var old in BackupRetention.ToDelete(List(), _keepCount))
            {
                File.Delete(Path.Combine(Folder, old.Name));
            }

            return new BackupFile(name, createdAt, kind, upgradeVersion, new FileInfo(path).Length);
        }
    }

    /// <summary>The backups, newest first. Files with other names are ignored and never deleted.</summary>
    public IReadOnlyList<BackupFile> List()
    {
        if (!Directory.Exists(Folder))
        {
            return Array.Empty<BackupFile>();
        }

        var backups = new List<BackupFile>();
        foreach (var file in new DirectoryInfo(Folder).EnumerateFiles("workspace-*.db"))
        {
            if (BackupFileName.TryParse(file.Name, file.Length, out var backup))
            {
                backups.Add(backup!);
            }
        }

        return BackupRetention.NewestFirst(backups);
    }

    /// <summary>The path of a backup, or null when the name is not the name of an existing backup.</summary>
    public string? PathOf(string name)
    {
        if (!BackupFileName.TryParse(name, 0, out _))
        {
            return null;
        }

        var path = Path.Combine(Folder, name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Removes what an interrupted backup left behind.</summary>
    public void DeleteLeftovers()
    {
        if (!Directory.Exists(Folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(Folder, "workspace-*.db.tmp*"))
        {
            File.Delete(file);
        }
    }

    private static void DeleteTemporary(string temporary)
    {
        File.Delete(temporary);
        File.Delete(temporary + "-journal");
        WorkspaceDatabase.DeleteSideFiles(temporary);
    }
}
