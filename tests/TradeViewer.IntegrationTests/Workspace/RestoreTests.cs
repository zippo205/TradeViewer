using Microsoft.Data.Sqlite;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Contracts.Workspace;
using TradeViewer.Infrastructure.Workspace;

namespace TradeViewer.IntegrationTests.Workspace;

/// <summary>WS3: a restore replaces the workspace while the application runs; a refused restore changes nothing.</summary>
public class RestoreTests : IDisposable
{
    private readonly WorkspaceFixture _workspace = new WorkspaceFixture();

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task A_restore_brings_back_the_state_of_the_backup_and_the_workspace_keeps_working()
    {
        _workspace.Start();
        await _workspace.SetAsync("lastChart", "\"old\"");
        var backup = await _workspace.Backups.CreateAsync(CancellationToken.None);
        await _workspace.SetAsync("lastChart", "\"new\"");
        await _workspace.SetAsync("addedLater", "1");

        var result = await _workspace.Backups.RestoreAsync(backup.Name, CancellationToken.None);

        Assert.Equal(RestoreStatus.Restored, result.Status);
        Assert.Equal("\"old\"", await _workspace.GetAsync("lastChart"));
        Assert.Null(await _workspace.GetAsync("addedLater"));

        // The restored workspace is written like before, and survives a restart.
        await _workspace.SetAsync("afterRestore", "2");
        _workspace.Start();
        Assert.Equal("\"old\"", await _workspace.GetAsync("lastChart"));
        Assert.Equal("2", await _workspace.GetAsync("afterRestore"));

        Assert.Equal("wal", WorkspaceFixture.Scalar(_workspace.Locations.WorkspaceDatabasePath, "PRAGMA journal_mode;"));
        Assert.Empty(_workspace.StrayFiles());
        Assert.NotNull(_workspace.Store.PathOf(backup.Name)); // the backup itself stays
    }

    [Fact]
    public async Task An_uploaded_backup_is_restored()
    {
        _workspace.Start();
        await _workspace.SetAsync("lastChart", "\"old\"");
        var backup = await _workspace.Backups.CreateAsync(CancellationToken.None);
        var bytes = File.ReadAllBytes(_workspace.Store.PathOf(backup.Name)!);
        await _workspace.SetAsync("lastChart", "\"new\"");

        var result = await _workspace.Backups.RestoreAsync(new MemoryStream(bytes), CancellationToken.None);

        Assert.Equal(RestoreStatus.Restored, result.Status);
        Assert.Equal("\"old\"", await _workspace.GetAsync("lastChart"));
        Assert.Empty(_workspace.StrayFiles());
    }

    [Fact]
    public async Task A_backup_of_an_older_version_is_upgraded_before_it_replaces_the_workspace()
    {
        _workspace.Start();
        await _workspace.SetAsync("lastChart", "\"old\"");
        var backup = await _workspace.Backups.CreateAsync(CancellationToken.None);

        var next = _workspace.Schema.LatestVersion + 1;
        _workspace.Start(WorkspaceFixture.ExtraScript(next, "CREATE TABLE notes (id INTEGER PRIMARY KEY, text TEXT);"));
        await _workspace.SetAsync("lastChart", "\"new\"");

        var result = await _workspace.Backups.RestoreAsync(backup.Name, CancellationToken.None);

        Assert.Equal(RestoreStatus.Restored, result.Status);
        Assert.Equal("\"old\"", await _workspace.GetAsync("lastChart"));
        using (var workspace = await _workspace.Database.OpenAsync(CancellationToken.None))
        {
            Assert.Equal(next, WorkspaceSchema.ReadVersion(workspace.Connection));
            using var command = workspace.CreateCommand("SELECT COUNT(*) FROM notes;");
            Assert.Equal(0L, command.ExecuteScalar());
        }
    }

    [Fact]
    public async Task An_unknown_name_restores_nothing()
    {
        _workspace.Start();
        await _workspace.SetAsync("lastChart", "\"kept\"");

        foreach (var name in new[] { "workspace-20200101-000000-daily.db", "../workspace.db", "workspace.db", "" })
        {
            var result = await _workspace.Backups.RestoreAsync(name, CancellationToken.None);
            Assert.Equal(RestoreStatus.NotFound, result.Status);
        }

        Assert.Equal("\"kept\"", await _workspace.GetAsync("lastChart"));
    }

    [Fact]
    public async Task A_file_that_is_not_a_database_is_refused_and_changes_nothing()
    {
        await AssertRefusedAsync(new MemoryStream(new byte[20000]), "not a SQLite database");
        await AssertRefusedAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(new string('x', 20000))), "not a SQLite database");
    }

    [Fact]
    public async Task An_empty_file_is_refused_and_changes_nothing()
    {
        await AssertRefusedAsync(new MemoryStream(), "not a backup of a TradeViewer workspace");
    }

    [Fact]
    public async Task Another_SQLite_database_is_refused_and_changes_nothing()
    {
        var other = Path.Combine(_workspace.DataPath, "..", "other.db");
        using (var connection = new SqliteConnection("Data Source=" + other + ";Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT, updated_at TEXT); PRAGMA user_version = 1;";
            command.ExecuteNonQuery();
        }

        await AssertRefusedAsync(new MemoryStream(File.ReadAllBytes(other)), "not a backup of a TradeViewer workspace");
    }

    [Fact]
    public async Task A_backup_of_a_newer_version_is_refused_and_changes_nothing()
    {
        var next = WorkspaceSchema.FromEmbeddedScripts().LatestVersion + 1;
        _workspace.Start(WorkspaceFixture.ExtraScript(next, "CREATE TABLE future (id INTEGER PRIMARY KEY);"));
        var newer = await _workspace.Backups.CreateAsync(CancellationToken.None);
        var bytes = File.ReadAllBytes(_workspace.Store.PathOf(newer.Name)!);

        // Back to this build with a fresh workspace.
        _workspace.Stop();
        File.Delete(_workspace.Locations.WorkspaceDatabasePath);

        await AssertRefusedAsync(new MemoryStream(bytes), "Run a newer version of TradeViewer");
    }

    [Fact]
    public async Task A_damaged_backup_is_refused_and_changes_nothing()
    {
        _workspace.Start();
        for (var index = 0; index < 200; index++)
        {
            await _workspace.SetAsync("key" + index, "\"" + new string('v', 500) + "\"");
        }

        var backup = await _workspace.Backups.CreateAsync(CancellationToken.None);
        var bytes = File.ReadAllBytes(_workspace.Store.PathOf(backup.Name)!);
        for (var index = 4096; index < bytes.Length; index++)
        {
            bytes[index] = 0x5A; // every page after the first is garbage
        }

        var result = await _workspace.Backups.RestoreAsync(new MemoryStream(bytes), CancellationToken.None);

        Assert.Equal(RestoreStatus.Invalid, result.Status);
        Assert.Equal("\"" + new string('v', 500) + "\"", await _workspace.GetAsync("key199"));
        Assert.Empty(_workspace.StrayFiles());
    }

    [Fact]
    public async Task A_file_over_the_upload_limit_is_refused_and_changes_nothing()
    {
        _workspace.Start();
        await _workspace.SetAsync("lastChart", "\"kept\"");

        var result = await _workspace.Backups.RestoreAsync(new ZeroStream(BackupLimits.MaxUploadBytes + 1), CancellationToken.None);

        Assert.Equal(RestoreStatus.TooLarge, result.Status);
        Assert.Equal("\"kept\"", await _workspace.GetAsync("lastChart"));
        Assert.Empty(_workspace.StrayFiles());
    }

    [Fact]
    public async Task A_restore_waits_until_the_workspace_is_not_in_use_and_others_wait_for_the_restore()
    {
        _workspace.Start();
        await _workspace.SetAsync("lastChart", "\"old\"");
        var backup = await _workspace.Backups.CreateAsync(CancellationToken.None);
        await _workspace.SetAsync("lastChart", "\"new\"");

        Task<RestoreResult> restore;
        Task<string?> readDuringRestore;
        using (var inUse = await _workspace.Database.OpenAsync(CancellationToken.None))
        {
            restore = Task.Run(() => _workspace.Backups.RestoreAsync(backup.Name, CancellationToken.None));
            await Task.Delay(150);
            Assert.False(restore.IsCompleted);

            // Arrives while the restore is pending: it must see the restored workspace, not fail.
            readDuringRestore = Task.Run(() => _workspace.GetAsync("lastChart"));
            await Task.Delay(100);
            Assert.False(readDuringRestore.IsCompleted);

            using var command = inUse.CreateCommand("SELECT value FROM settings WHERE key = 'lastChart';");
            Assert.Equal("\"new\"", command.ExecuteScalar());
        }

        Assert.Equal(RestoreStatus.Restored, (await restore.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        Assert.Equal("\"old\"", await readDuringRestore.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Writes_and_restores_at_the_same_time_never_fail()
    {
        _workspace.Start();
        await _workspace.SetAsync("counter", "0");
        var backup = await _workspace.Backups.CreateAsync(CancellationToken.None);

        var writers = Enumerable.Range(0, 4).Select(writer => Task.Run(async () =>
        {
            for (var index = 0; index < 100; index++)
            {
                await _workspace.SetAsync("writer" + writer, index.ToString());
                await _workspace.GetAsync("counter");
            }
        })).ToList();

        var restores = Task.Run(async () =>
        {
            for (var index = 0; index < 10; index++)
            {
                Assert.Equal(RestoreStatus.Restored, (await _workspace.Backups.RestoreAsync(backup.Name, CancellationToken.None)).Status);
            }
        });

        await Task.WhenAll(writers.Append(restores)).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal("0", await _workspace.GetAsync("counter"));
        Assert.Equal("ok", WorkspaceFixture.Scalar(_workspace.Locations.WorkspaceDatabasePath, "PRAGMA integrity_check;"));
    }

    private async Task AssertRefusedAsync(Stream upload, string expectedMessage)
    {
        _workspace.Start();
        await _workspace.SetAsync("lastChart", "\"kept\"");

        var result = await _workspace.Backups.RestoreAsync(upload, CancellationToken.None);

        Assert.Equal(RestoreStatus.Invalid, result.Status);
        Assert.Contains(expectedMessage, result.Message);
        Assert.Equal("\"kept\"", await _workspace.GetAsync("lastChart"));
        Assert.Empty(_workspace.StrayFiles());
    }

    /// <summary>A stream of zeros of a given length, without holding them in memory.</summary>
    private sealed class ZeroStream : Stream
    {
        private long _remaining;

        public ZeroStream(long length)
        {
            _remaining = length;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(count, _remaining);
            Array.Clear(buffer, offset, read);
            _remaining -= read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
