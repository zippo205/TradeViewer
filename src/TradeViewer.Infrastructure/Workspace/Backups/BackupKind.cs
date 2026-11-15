namespace TradeViewer.Infrastructure.Workspace.Backups;

public enum BackupKind
{
    /// <summary>The backup of the day (WS3).</summary>
    Daily,

    /// <summary>Made by the user with "Back up now".</summary>
    Manual,

    /// <summary>Made before the schema was upgraded (WS4).</summary>
    BeforeUpgrade
}
