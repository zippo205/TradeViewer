using TradeViewer.Infrastructure.Workspace.Backups;

namespace TradeViewer.UnitTests.Infrastructure.Workspace;

public class BackupFileNameTests
{
    private static readonly DateTimeOffset Time = new DateTimeOffset(2026, 11, 15, 3, 0, 7, TimeSpan.Zero);

    [Fact]
    public void The_name_tells_the_time_in_UTC_and_the_kind()
    {
        Assert.Equal("workspace-20261115-030007-daily.db", BackupFileName.Create(Time, BackupKind.Daily, null));
        Assert.Equal("workspace-20261115-030007-manual.db", BackupFileName.Create(Time, BackupKind.Manual, null));
        Assert.Equal("workspace-20261115-030007-before-v3.db", BackupFileName.Create(Time, BackupKind.BeforeUpgrade, 3));
    }

    [Fact]
    public void A_local_time_is_written_as_UTC()
    {
        var local = new DateTimeOffset(2026, 11, 15, 5, 0, 7, TimeSpan.FromHours(2));

        Assert.Equal("workspace-20261115-030007-daily.db", BackupFileName.Create(local, BackupKind.Daily, null));
    }

    [Fact]
    public void A_backup_before_an_upgrade_needs_its_version()
    {
        Assert.Throws<ArgumentException>(() => BackupFileName.Create(Time, BackupKind.BeforeUpgrade, null));
    }

    [Theory]
    [InlineData(BackupKind.Daily, null)]
    [InlineData(BackupKind.Manual, null)]
    [InlineData(BackupKind.BeforeUpgrade, 12)]
    public void A_name_is_read_back(BackupKind kind, int? version)
    {
        var name = BackupFileName.Create(Time, kind, version);

        Assert.True(BackupFileName.TryParse(name, 4096, out var backup));
        Assert.Equal(name, backup!.Name);
        Assert.Equal(Time, backup.CreatedAt);
        Assert.Equal(kind, backup.Kind);
        Assert.Equal(version, backup.UpgradeVersion);
        Assert.Equal(4096, backup.SizeBytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("workspace.db")]
    [InlineData("../workspace.db")]
    [InlineData("workspace-20261115-030007-daily.db.tmp")]
    [InlineData("workspace-20261115-030007-weekly.db")]
    [InlineData("workspace-20261115-030007-before-v.db")]
    [InlineData("workspace-20261315-030007-daily.db")]
    [InlineData("workspace-20261115-030007-daily.db/../../workspace.db")]
    [InlineData("x/workspace-20261115-030007-daily.db")]
    [InlineData("workspace-20261115-030007-daily.db\n")]
    public void Any_other_name_is_not_a_backup(string? name)
    {
        Assert.False(BackupFileName.TryParse(name, 0, out var backup));
        Assert.Null(backup);
    }
}
