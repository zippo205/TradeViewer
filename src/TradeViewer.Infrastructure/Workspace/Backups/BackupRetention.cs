namespace TradeViewer.Infrastructure.Workspace.Backups;

/// <summary>One rule for all backups: the newest are kept, whatever their kind (WS3).</summary>
public static class BackupRetention
{
    /// <summary>Newest first; two backups of the same second are ordered by name, so the order is always the same.</summary>
    public static IReadOnlyList<BackupFile> NewestFirst(IEnumerable<BackupFile> backups)
    {
        return backups
            .OrderByDescending(backup => backup.CreatedAt)
            .ThenByDescending(backup => backup.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<BackupFile> ToDelete(IEnumerable<BackupFile> backups, int keepCount)
    {
        return NewestFirst(backups).Skip(Math.Max(keepCount, 1)).ToList();
    }
}
