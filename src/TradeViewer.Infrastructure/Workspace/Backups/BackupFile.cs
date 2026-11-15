namespace TradeViewer.Infrastructure.Workspace.Backups;

/// <summary>A backup in the backups folder, as its file name tells it.</summary>
public sealed class BackupFile
{
    public BackupFile(string name, DateTimeOffset createdAt, BackupKind kind, int? upgradeVersion, long sizeBytes)
    {
        Name = name;
        CreatedAt = createdAt;
        Kind = kind;
        UpgradeVersion = upgradeVersion;
        SizeBytes = sizeBytes;
    }

    public string Name { get; }

    /// <summary>UTC, to the second.</summary>
    public DateTimeOffset CreatedAt { get; }

    public BackupKind Kind { get; }

    /// <summary>For <see cref="BackupKind.BeforeUpgrade"/>: the schema version the upgrade led to.</summary>
    public int? UpgradeVersion { get; }

    public long SizeBytes { get; }
}
