namespace TradeViewer.Contracts.Workspace;

public static class BackupLimits
{
    /// <summary>The largest backup file that can be uploaded for a restore: 256 MB.</summary>
    public const long MaxUploadBytes = 256L * 1024 * 1024;
}
