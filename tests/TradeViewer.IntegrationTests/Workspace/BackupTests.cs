using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Infrastructure.Workspace.Backups;

namespace TradeViewer.IntegrationTests.Workspace;

/// <summary>WS3: backups are complete copies, only the newest are kept, and the daily one is made when due.</summary>
public class BackupTests : IDisposable
{
    private readonly WorkspaceFixture _workspace = new WorkspaceFixture();

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task A_backup_is_a_complete_copy_of_the_workspace()
    {
        _workspace.Start();
        await _workspace.SetAsync("lastChart", "\"binance/BTCUSDT\"");

        var backup = await _workspace.Backups.CreateAsync(CancellationToken.None);
        await _workspace.SetAsync("lastChart", "\"later\"");

        Assert.Equal("workspace-20261115-120000-manual.db", backup.Name);
        Assert.Equal("manual", backup.Kind);
        Assert.Equal(_workspace.Clock.Now, backup.CreatedAt);

        var path = Path.Combine(_workspace.Locations.BackupsPath, backup.Name);
        Assert.Equal(new FileInfo(path).Length, backup.SizeBytes);
        Assert.Equal("ok", WorkspaceFixture.Scalar(path, "PRAGMA integrity_check;"));
        Assert.Equal("\"binance/BTCUSDT\"", WorkspaceFixture.Scalar(path, "SELECT value FROM settings WHERE key = 'lastChart';"));

        var listed = Assert.Single(await _workspace.Backups.ListAsync(CancellationToken.None));
        Assert.Equal(backup, listed);
        Assert.Equal(new[] { backup.Name }, Directory.EnumerateFiles(_workspace.Locations.BackupsPath).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Two_backups_in_the_same_second_get_different_names()
    {
        _workspace.Start();

        var first = await _workspace.Backups.CreateAsync(CancellationToken.None);
        var second = await _workspace.Backups.CreateAsync(CancellationToken.None);

        Assert.NotEqual(first.Name, second.Name);
        Assert.Equal(2, _workspace.Store.List().Count);
    }

    [Fact]
    public async Task Only_the_newest_backups_are_kept_and_other_files_are_left_alone()
    {
        _workspace.KeepCount = 3;
        _workspace.Start();
        Directory.CreateDirectory(_workspace.Locations.BackupsPath);
        var foreign = Path.Combine(_workspace.Locations.BackupsPath, "workspace-notes.db");
        File.WriteAllText(foreign, "not a backup");

        var names = new List<string>();
        for (var day = 0; day < 5; day++)
        {
            var kind = day == 1 ? BackupKind.Manual : BackupKind.Daily;
            names.Add((await _workspace.Backups.CreateAsync(kind, CancellationToken.None)).Name);
            _workspace.Clock.Now = _workspace.Clock.Now.AddDays(1);
        }

        Assert.Equal(new[] { names[4], names[3], names[2] }, _workspace.Store.List().Select(backup => backup.Name));
        Assert.True(File.Exists(foreign));
    }

    [Fact]
    public async Task A_backup_is_opened_for_download_by_its_name_only()
    {
        _workspace.Start();
        var backup = await _workspace.Backups.CreateAsync(CancellationToken.None);

        var file = await _workspace.Backups.OpenAsync(backup.Name, CancellationToken.None);
        Assert.NotNull(file);
        using (file!.Content)
        {
            Assert.Equal(backup.Name, file.FileName);
            Assert.Equal(backup.SizeBytes, file.Content.Length);
        }

        Assert.Null(await _workspace.Backups.OpenAsync("workspace-20200101-000000-daily.db", CancellationToken.None));
        Assert.Null(await _workspace.Backups.OpenAsync("../workspace.db", CancellationToken.None));
        Assert.Null(await _workspace.Backups.OpenAsync("../" + WorkspaceFixtureFileName(), CancellationToken.None));
    }

    [Fact]
    public async Task The_daily_backup_is_made_when_none_exists_and_then_once_per_day()
    {
        _workspace.Start();
        var service = _workspace.DailyService("03:00");
        _workspace.Clock.Now = Utc(15, 12, 0);

        Assert.True(await service.BackupIfDueAsync(CancellationToken.None));
        Assert.False(await service.BackupIfDueAsync(CancellationToken.None));

        _workspace.Clock.Now = Utc(16, 2, 59);
        Assert.False(await service.BackupIfDueAsync(CancellationToken.None));

        _workspace.Clock.Now = Utc(16, 3, 0);
        Assert.True(await service.BackupIfDueAsync(CancellationToken.None));
        Assert.False(await service.BackupIfDueAsync(CancellationToken.None));

        Assert.Equal(
            new[] { "workspace-20261116-030000-daily.db", "workspace-20261115-120000-daily.db" },
            _workspace.Store.List().Select(backup => backup.Name));
    }

    [Fact]
    public async Task A_missed_daily_backup_is_made_at_the_next_start()
    {
        _workspace.Start();
        _workspace.Clock.Now = Utc(14, 3, 0);
        await _workspace.Backups.CreateAsync(BackupKind.Daily, CancellationToken.None);

        // The machine was off from the 14th to the 17th.
        _workspace.Clock.Now = Utc(17, 9, 30);
        _workspace.Start();

        Assert.True(await _workspace.DailyService("03:00").BackupIfDueAsync(CancellationToken.None));
        Assert.Equal("workspace-20261117-093000-daily.db", _workspace.Store.List()[0].Name);
    }

    [Fact]
    public async Task A_backup_made_by_the_user_today_counts_for_the_day()
    {
        _workspace.Start();
        _workspace.Clock.Now = Utc(15, 4, 0);
        await _workspace.Backups.CreateAsync(CancellationToken.None);

        _workspace.Clock.Now = Utc(15, 5, 0);

        Assert.False(await _workspace.DailyService("03:00").BackupIfDueAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_service_backs_up_at_its_start_and_stops_cleanly()
    {
        _workspace.Start();
        using var service = _workspace.DailyService("03:00");

        await service.StartAsync(CancellationToken.None);
        for (var attempt = 0; attempt < 100 && _workspace.Store.List().Count == 0; attempt++)
        {
            await Task.Delay(20);
        }

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(BackupKind.Daily, Assert.Single(_workspace.Store.List()).Kind);
    }

    private static string WorkspaceFixtureFileName()
    {
        return TradeViewer.Infrastructure.Workspace.WorkspaceDatabase.FileName;
    }

    private static DateTimeOffset Utc(int day, int hour, int minute)
    {
        return new DateTimeOffset(2026, 11, day, hour, minute, 0, TimeSpan.Zero);
    }
}
