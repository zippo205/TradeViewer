using TradeViewer.Infrastructure.Workspace;

namespace TradeViewer.IntegrationTests.Workspace;

/// <summary>WS1, WS2, WS4: the workspace is created, written at once, survives a restart and an update.</summary>
public class WorkspaceStartTests : IDisposable
{
    private readonly WorkspaceFixture _workspace = new WorkspaceFixture();

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public void The_first_start_creates_the_workspace_at_the_latest_version()
    {
        var version = _workspace.Start();

        var path = _workspace.Locations.WorkspaceDatabasePath;
        Assert.Equal(_workspace.Schema.LatestVersion, version);
        Assert.True(File.Exists(path));
        Assert.Equal("wal", WorkspaceFixture.Scalar(path, "PRAGMA journal_mode;"));
        Assert.Equal(WorkspaceDatabase.ApplicationId.ToString(), WorkspaceFixture.Scalar(path, "PRAGMA application_id;"));
        Assert.Empty(_workspace.Store.List());
    }

    [Fact]
    public async Task Every_connection_waits_for_the_disk()
    {
        _workspace.Start();

        using var workspace = await _workspace.Database.OpenAsync(CancellationToken.None);
        using var command = workspace.CreateCommand("PRAGMA synchronous;");

        Assert.Equal(2L, command.ExecuteScalar()); // 2 = FULL
    }

    [Fact]
    public async Task A_setting_is_saved_at_once_and_survives_a_restart()
    {
        _workspace.Start();
        await _workspace.SetAsync("defaultHistory", "{\"years\":10}");
        await _workspace.SetAsync("defaultHistory", "{\"years\":5}");

        _workspace.Start();

        Assert.Equal("{\"years\":5}", await _workspace.GetAsync("defaultHistory"));
        Assert.Null(await _workspace.GetAsync("neverWritten"));
        Assert.Empty(_workspace.Store.List()); // no upgrade, no backup
    }

    [Fact]
    public async Task An_update_upgrades_the_workspace_keeps_the_data_and_backs_it_up_before()
    {
        _workspace.Start();
        await _workspace.SetAsync("lastChart", "\"binance/BTCUSDT\"");
        var next = _workspace.Schema.LatestVersion + 1;

        var version = _workspace.Start(
            WorkspaceFixture.ExtraScript(next, "CREATE TABLE notes (id INTEGER PRIMARY KEY, text TEXT NOT NULL); INSERT INTO notes (text) VALUES ('new');"),
            WorkspaceFixture.ExtraScript(next + 1, "ALTER TABLE settings ADD COLUMN note TEXT;"));

        Assert.Equal(next + 1, version);
        Assert.Equal("\"binance/BTCUSDT\"", await _workspace.GetAsync("lastChart"));
        Assert.Equal("new", WorkspaceFixture.Scalar(_workspace.Locations.WorkspaceDatabasePath, "SELECT text FROM notes;"));

        var backup = Assert.Single(_workspace.Store.List());
        Assert.EndsWith("-before-v" + (next + 1) + ".db", backup.Name);
        var backupPath = Path.Combine(_workspace.Locations.BackupsPath, backup.Name);
        Assert.Equal((next - 1).ToString(), WorkspaceFixture.Scalar(backupPath, "PRAGMA user_version;"));
        Assert.Equal("\"binance/BTCUSDT\"", WorkspaceFixture.Scalar(backupPath, "SELECT value FROM settings WHERE key = 'lastChart';"));
    }

    [Fact]
    public async Task A_failing_script_leaves_the_workspace_at_the_version_before_it()
    {
        _workspace.Start();
        await _workspace.SetAsync("lastChart", "\"binance/ETHUSDT\"");
        var next = _workspace.Schema.LatestVersion + 1;

        Assert.ThrowsAny<Exception>(() => _workspace.Start(
            WorkspaceFixture.ExtraScript(next, "CREATE TABLE good (id INTEGER PRIMARY KEY);"),
            WorkspaceFixture.ExtraScript(next + 1, "CREATE TABLE half (id INTEGER PRIMARY KEY); INSERT INTO missing_table VALUES (1);")));

        var path = _workspace.Locations.WorkspaceDatabasePath;
        _workspace.Stop();
        Assert.Equal(next.ToString(), WorkspaceFixture.Scalar(path, "PRAGMA user_version;"));
        Assert.Equal("0", WorkspaceFixture.Scalar(path, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'half';"));
        Assert.Equal("\"binance/ETHUSDT\"", WorkspaceFixture.Scalar(path, "SELECT value FROM settings WHERE key = 'lastChart';"));
    }

    [Fact]
    public async Task A_workspace_of_a_newer_version_is_refused_and_not_touched()
    {
        var next = WorkspaceSchema.FromEmbeddedScripts().LatestVersion + 1;
        _workspace.Start(WorkspaceFixture.ExtraScript(next, "CREATE TABLE future (id INTEGER PRIMARY KEY);"));
        await _workspace.SetAsync("lastChart", "\"x\"");

        var exception = Assert.Throws<InvalidOperationException>(() => _workspace.Start());

        Assert.Contains("Run a newer version of TradeViewer", exception.Message);
        _workspace.Stop();
        Assert.Equal(next.ToString(), WorkspaceFixture.Scalar(_workspace.Locations.WorkspaceDatabasePath, "PRAGMA user_version;"));
        Assert.Empty(_workspace.Store.List());
    }

    [Fact]
    public void A_damaged_workspace_stops_the_start_and_is_not_replaced()
    {
        var path = _workspace.Locations.WorkspaceDatabasePath;
        File.WriteAllText(path, new string('x', 5000));

        var exception = Assert.Throws<InvalidOperationException>(() => _workspace.Start());

        Assert.Contains("is damaged", exception.Message);
        Assert.Equal(new string('x', 5000), File.ReadAllText(path));
    }

    [Fact]
    public void Leftovers_of_an_interrupted_restore_or_backup_are_removed_at_the_start()
    {
        Directory.CreateDirectory(_workspace.Locations.BackupsPath);
        var leftovers = new[]
        {
            Path.Combine(_workspace.DataPath, "restore-abc.tmp"),
            Path.Combine(_workspace.DataPath, "restore-abc.tmp-wal"),
            Path.Combine(_workspace.DataPath, "upload-abc.tmp"),
            Path.Combine(_workspace.Locations.BackupsPath, "workspace-20261115-030000-daily.db.tmp")
        };
        var foreign = Path.Combine(_workspace.DataPath, "marketdata.db");
        foreach (var file in leftovers.Append(foreign))
        {
            File.WriteAllText(file, "x");
        }

        _workspace.Start();

        Assert.All(leftovers, file => Assert.False(File.Exists(file)));
        Assert.True(File.Exists(foreign));
    }
}
