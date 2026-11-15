using TradeViewer.Infrastructure.Workspace.Backups;

namespace TradeViewer.UnitTests.Infrastructure.Workspace;

public class BackupRetentionTests
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_newest_backups_are_kept_whatever_their_kind()
    {
        var backups = new[]
        {
            Backup(0, BackupKind.Daily),
            Backup(1, BackupKind.BeforeUpgrade),
            Backup(2, BackupKind.Daily),
            Backup(3, BackupKind.Manual),
            Backup(4, BackupKind.Daily)
        };

        var deleted = BackupRetention.ToDelete(backups.Reverse(), 3);

        Assert.Equal(new[] { backups[1].Name, backups[0].Name }, deleted.Select(backup => backup.Name));
    }

    [Fact]
    public void Nothing_is_deleted_up_to_the_keep_count()
    {
        var backups = new[] { Backup(0, BackupKind.Daily), Backup(1, BackupKind.Daily) };

        Assert.Empty(BackupRetention.ToDelete(backups, 2));
        Assert.Empty(BackupRetention.ToDelete(backups, 14));
        Assert.Empty(BackupRetention.ToDelete(Array.Empty<BackupFile>(), 14));
    }

    [Fact]
    public void The_newest_backup_is_never_deleted()
    {
        var backups = new[] { Backup(0, BackupKind.Daily), Backup(1, BackupKind.Daily) };

        var deleted = BackupRetention.ToDelete(backups, 0);

        Assert.Equal(new[] { backups[0].Name }, deleted.Select(backup => backup.Name));
    }

    [Fact]
    public void The_list_is_newest_first()
    {
        var backups = new[] { Backup(1, BackupKind.Daily), Backup(3, BackupKind.Manual), Backup(2, BackupKind.Daily) };

        var ordered = BackupRetention.NewestFirst(backups);

        Assert.Equal(new[] { backups[1].Name, backups[2].Name, backups[0].Name }, ordered.Select(backup => backup.Name));
    }

    private static BackupFile Backup(int day, BackupKind kind)
    {
        var time = Start.AddDays(day);
        int? version = kind == BackupKind.BeforeUpgrade ? 2 : null;
        return new BackupFile(BackupFileName.Create(time, kind, version), time, kind, version, 4096);
    }
}
