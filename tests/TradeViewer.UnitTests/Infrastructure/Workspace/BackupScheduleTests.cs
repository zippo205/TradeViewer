using TradeViewer.Infrastructure.Workspace.Backups;

namespace TradeViewer.UnitTests.Infrastructure.Workspace;

public class BackupScheduleTests
{
    private static readonly TimeOnly Three = new TimeOnly(3, 0);

    [Fact]
    public void Before_the_time_of_day_the_last_due_time_is_yesterday()
    {
        Assert.Equal(Utc(14, 3, 0), BackupSchedule.LastDue(Utc(15, 2, 59), Three));
        Assert.Equal(Utc(15, 3, 0), BackupSchedule.NextDue(Utc(15, 2, 59), Three));
    }

    [Fact]
    public void At_and_after_the_time_of_day_the_last_due_time_is_today()
    {
        Assert.Equal(Utc(15, 3, 0), BackupSchedule.LastDue(Utc(15, 3, 0), Three));
        Assert.Equal(Utc(15, 3, 0), BackupSchedule.LastDue(Utc(15, 23, 30), Three));
        Assert.Equal(Utc(16, 3, 0), BackupSchedule.NextDue(Utc(15, 3, 0), Three));
    }

    [Fact]
    public void The_due_times_are_UTC_whatever_the_offset_of_now()
    {
        var now = new DateTimeOffset(2026, 11, 15, 4, 30, 0, TimeSpan.FromHours(2)); // 02:30 UTC

        Assert.Equal(Utc(14, 3, 0), BackupSchedule.LastDue(now, Three));
    }

    [Fact]
    public void Without_a_backup_one_is_due()
    {
        Assert.True(BackupSchedule.IsDue(Utc(15, 12, 0), Three, null));
    }

    [Fact]
    public void A_backup_since_the_last_due_time_is_enough()
    {
        Assert.False(BackupSchedule.IsDue(Utc(15, 12, 0), Three, Utc(15, 3, 0)));
        Assert.False(BackupSchedule.IsDue(Utc(15, 12, 0), Three, Utc(15, 9, 0)));
        Assert.False(BackupSchedule.IsDue(Utc(15, 2, 0), Three, Utc(14, 3, 0)));
    }

    [Fact]
    public void A_missed_backup_is_due_at_once()
    {
        // The machine was off at 03:00: the newest backup is from yesterday.
        Assert.True(BackupSchedule.IsDue(Utc(15, 8, 0), Three, Utc(14, 3, 0)));
        Assert.True(BackupSchedule.IsDue(Utc(15, 3, 0), Three, Utc(14, 3, 0)));
    }

    private static DateTimeOffset Utc(int day, int hour, int minute)
    {
        return new DateTimeOffset(2026, 11, day, hour, minute, 0, TimeSpan.Zero);
    }
}
