namespace TradeViewer.Infrastructure.Workspace.Backups;

/// <summary>The due times of the daily backup: every day at the configured time of day, UTC.</summary>
public static class BackupSchedule
{
    /// <summary>The latest due time that is not after now.</summary>
    public static DateTimeOffset LastDue(DateTimeOffset now, TimeOnly timeOfDay)
    {
        var utc = now.ToUniversalTime();
        var today = new DateTimeOffset(utc.Year, utc.Month, utc.Day, timeOfDay.Hour, timeOfDay.Minute, 0, TimeSpan.Zero);
        return today <= utc ? today : today.AddDays(-1);
    }

    /// <summary>The first due time after now.</summary>
    public static DateTimeOffset NextDue(DateTimeOffset now, TimeOnly timeOfDay)
    {
        return LastDue(now, timeOfDay).AddDays(1);
    }

    /// <summary>A backup is due when there is none or the newest one is older than the last due time (also after the machine was off).</summary>
    public static bool IsDue(DateTimeOffset now, TimeOnly timeOfDay, DateTimeOffset? newestBackup)
    {
        return newestBackup == null || newestBackup.Value < LastDue(now, timeOfDay);
    }
}
